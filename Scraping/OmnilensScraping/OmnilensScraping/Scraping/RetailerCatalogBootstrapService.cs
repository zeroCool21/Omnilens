using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml.Linq;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class RetailerCatalogBootstrapService
{
    private const string SnapshotFilePrefix = "catalog-bootstrap-product";
    private const string MetadataFileName = "catalog-bootstrap.metadata.json";

    private static readonly string[] IgnoredExtensions =
    {
        ".css",
        ".gif",
        ".ico",
        ".jpg",
        ".jpeg",
        ".js",
        ".json",
        ".pdf",
        ".png",
        ".svg",
        ".webp",
        ".xml",
        ".zip"
    };

    private readonly PageContentService _pageContentService;
    private readonly CatalogBootstrapOptions _options;
    private readonly CatalogRefreshOptions _refreshOptions;
    private readonly ILogger<RetailerCatalogBootstrapService> _logger;
    private readonly ConcurrentDictionary<RetailerType, SemaphoreSlim> _bootstrapLocks = new();

    public RetailerCatalogBootstrapService(
        PageContentService pageContentService,
        IOptions<CatalogBootstrapOptions> options,
        IOptions<CatalogRefreshOptions> refreshOptions,
        ILogger<RetailerCatalogBootstrapService> logger)
    {
        _pageContentService = pageContentService;
        _options = options.Value;
        _refreshOptions = refreshOptions.Value;
        _logger = logger;
    }

    public bool CanBootstrap(RetailerDefinition definition)
    {
        return definition.Retailer != RetailerType.AmazonIt &&
               definition.BootstrapSeedUrls.Count > 0;
    }

    public async Task<RetailerCatalogBootstrapResponse?> EnsurePublicSnapshotAsync(
        RetailerDefinition definition,
        bool force = false,
        int take = 0,
        CancellationToken cancellationToken = default)
    {
        if (!force && HasPublicSnapshot(definition) && IsPublicSnapshotFresh(definition))
        {
            return null;
        }

        return await BootstrapAsync(definition, force, take, cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetOrRefreshSnapshotUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        if (IsPublicSnapshotFresh(definition))
        {
            var cachedUrls = await ReadSnapshotUrlsAsync(definition, cancellationToken);
            if (cachedUrls.Count > 0)
            {
                return cachedUrls;
            }
        }

        await BootstrapAsync(definition, force: false, take: 0, cancellationToken);
        return await ReadSnapshotUrlsAsync(definition, cancellationToken);
    }

    public async Task<int> GetOrRefreshProductSourceCountAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var metadata = await TryReadMetadataAsync(definition, cancellationToken);
        if (metadata is not null && IsPublicSnapshotFresh(definition))
        {
            return metadata.GeneratedSitemaps;
        }

        await BootstrapAsync(definition, force: false, take: 0, cancellationToken);
        metadata = await TryReadMetadataAsync(definition, cancellationToken);
        return metadata?.GeneratedSitemaps ?? ResolvePersistedSitemapFiles(definition).Count;
    }

    public bool HasPublicSnapshot(RetailerDefinition definition)
    {
        return ResolvePersistedSitemapFiles(definition).Count > 0;
    }

    public bool IsPublicSnapshotFresh(RetailerDefinition definition)
    {
        var markerPath = ResolveFreshnessMarkerPath(definition);
        if (string.IsNullOrWhiteSpace(markerPath) || !File.Exists(markerPath))
        {
            return false;
        }

        var staleAfter = Math.Max(1, _refreshOptions.SnapshotStaleAfterMinutes);
        return DateTime.UtcNow - File.GetLastWriteTimeUtc(markerPath) < TimeSpan.FromMinutes(staleAfter);
    }

    public string ResolveOutputDirectory(RetailerDefinition definition)
    {
        var root = string.IsNullOrWhiteSpace(_refreshOptions.SnapshotRootDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "CatalogSnapshots")
            : _refreshOptions.SnapshotRootDirectory;

        return Path.Combine(Path.GetFullPath(root), definition.Retailer.ToString(), "bootstrap");
    }

    public async Task<RetailerCatalogBootstrapResponse> BootstrapAsync(
        RetailerDefinition definition,
        bool force,
        int take = 0,
        CancellationToken cancellationToken = default)
    {
        if (!CanBootstrap(definition))
        {
            throw new NotSupportedException($"Il retailer {definition.DisplayName} non espone un bootstrap pubblico configurato.");
        }

        var bootstrapLock = _bootstrapLocks.GetOrAdd(
            definition.Retailer,
            static _ => new SemaphoreSlim(1, 1));

        await bootstrapLock.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var warnings = new ConcurrentQueue<string>();
            var requestedTake = take > 0 ? take : 0;
            var queue = new Queue<Uri>(definition.BootstrapSeedUrls.Select(url => new Uri(url)));
            var scheduledPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var crawledPages = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var productUrls = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var stoppedForTakeLimit = false;

            foreach (var seed in definition.BootstrapSeedUrls)
            {
                scheduledPages.Add(NormalizePageKey(new Uri(seed)));
            }

            while (queue.Count > 0 && crawledPages.Count < ResolveMaxPages(definition))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remainingCapacity = ResolveMaxPages(definition) - crawledPages.Count;
                var batchSize = Math.Min(
                    ResolveMaxConcurrency(definition),
                    Math.Min(queue.Count, remainingCapacity));

                var batch = new List<Uri>(batchSize);
                while (batch.Count < batchSize && queue.Count > 0)
                {
                    batch.Add(queue.Dequeue());
                }

                var discoveredPages = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
                await Parallel.ForEachAsync(
                    batch,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = ResolveMaxConcurrency(definition),
                        CancellationToken = cancellationToken
                    },
                    async (pageUri, ct) =>
                    {
                        var pageKey = NormalizePageKey(pageUri);
                        crawledPages.TryAdd(pageKey, 0);

                        try
                        {
                            var scanResult = await ScanPageWithRetryAsync(definition, pageUri, ct);
                            foreach (var productUrl in scanResult.ProductUrls)
                            {
                                productUrls.TryAdd(productUrl, 0);
                            }

                            foreach (var nextPage in scanResult.DiscoveryPages)
                            {
                                discoveredPages.TryAdd(NormalizePageKey(nextPage), nextPage);
                            }
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            warnings.Enqueue($"{pageUri}: {exception.Message}");
                        }
                    });

                foreach (var discoveredPage in discoveredPages.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (scheduledPages.Count >= ResolveMaxPages(definition))
                    {
                        break;
                    }

                    if (scheduledPages.Add(discoveredPage.Key))
                    {
                        queue.Enqueue(discoveredPage.Value);
                    }
                }

                if (requestedTake > 0 && productUrls.Count >= requestedTake)
                {
                    stoppedForTakeLimit = true;
                    warnings.Enqueue($"Raggiunto il limite take={requestedTake}. La snapshot verra salvata con al massimo {requestedTake} prodotti.");
                    break;
                }
            }

            if (!stoppedForTakeLimit && queue.Count > 0)
            {
                warnings.Enqueue($"Bootstrap interrotto al limite di {ResolveMaxPages(definition)} pagine. Aumenta i limiti del bootstrap se vuoi esplorare piu a fondo.");
            }

            var persistedUrls = productUrls.Keys
                .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
                .Take(requestedTake > 0 ? requestedTake : int.MaxValue)
                .ToArray();

            var generatedSitemaps = persistedUrls.Length > 0
                ? await PersistSnapshotAsync(
                    definition,
                    persistedUrls,
                    new BootstrapSnapshotMetadata
                    {
                        GeneratedSitemaps = Math.Max(1, (int)Math.Ceiling(persistedUrls.Length / 50000d)),
                        CrawledPages = crawledPages.Count,
                        EnqueuedPages = scheduledPages.Count,
                        DiscoveredProducts = productUrls.Count,
                        PersistedProducts = persistedUrls.Length,
                        RequestedTake = requestedTake > 0 ? requestedTake : null,
                        RefreshedAtUtc = DateTimeOffset.UtcNow
                    },
                    cancellationToken)
                : Array.Empty<string>();

            if (persistedUrls.Length == 0)
            {
                warnings.Enqueue("Il bootstrap non ha prodotto una snapshot valida.");
            }

            stopwatch.Stop();

            return new RetailerCatalogBootstrapResponse
            {
                Success = persistedUrls.Length > 0,
                Retailer = definition.Retailer,
                OutputDirectory = ResolveOutputDirectory(definition),
                CrawledPages = crawledPages.Count,
                EnqueuedPages = scheduledPages.Count,
                GeneratedSitemaps = generatedSitemaps.Count,
                DiscoveredProducts = productUrls.Count,
                PersistedProducts = persistedUrls.Length,
                DiscoveredCategories = 0,
                RequestedTake = requestedTake > 0 ? requestedTake : null,
                DurationMs = stopwatch.ElapsedMilliseconds,
                SitemapFiles = generatedSitemaps,
                Warnings = warnings.ToArray()
            };
        }
        finally
        {
            bootstrapLock.Release();
        }
    }

    private async Task<IReadOnlyCollection<string>> ReadSnapshotUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var files = ResolvePersistedSitemapFiles(definition);
        if (files.Count == 0)
        {
            return Array.Empty<string>();
        }

        var urls = new List<string>();
        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            urls.AddRange(ParseLocations(content));
        }

        return urls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<BootstrapPageScanResult> ScanPageWithRetryAsync(
        RetailerDefinition definition,
        Uri pageUri,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        var maxAttempts = Math.Max(1, _options.MaxRetryAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DelayBeforeRequestAsync(cancellationToken);

            try
            {
                var useBrowser = definition.BootstrapUseBrowser || attempt == maxAttempts;
                var html = await _pageContentService.GetHtmlAsync(pageUri, useBrowser, cancellationToken);
                if (LooksLikeChallenge(html))
                {
                    throw new RetryableBootstrapException("Pagina bloccata da challenge o accesso negato.");
                }

                var document = HtmlHelpers.Load(html);
                var productUrls = ExtractProductUrls(document, pageUri, definition);
                var discoveryPages = ExtractDiscoveryPages(document, pageUri, definition);

                if (productUrls.Count == 0 && discoveryPages.Count == 0)
                {
                    throw new RetryableBootstrapException("Pagina discovery vuota o senza link utili.");
                }

                return new BootstrapPageScanResult(productUrls, discoveryPages);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < maxAttempts)
            {
                lastError = exception;
                _logger.LogDebug(exception, "Retry bootstrap {Retailer} {Attempt}/{MaxAttempts} su {Url}", definition.DisplayName, attempt, maxAttempts, pageUri);
                await DelayForRetryAsync(attempt, cancellationToken);
            }
            catch (Exception exception)
            {
                lastError = exception;
                break;
            }
        }

        throw lastError ?? new InvalidOperationException($"Impossibile leggere la discovery page {pageUri}.");
    }

    private static IReadOnlyCollection<string> ExtractProductUrls(
        HtmlDocument document,
        Uri pageUri,
        RetailerDefinition definition)
    {
        return document.DocumentNode
            .SelectNodes("//a[@href]")
            ?.Select(node => HtmlHelpers.NormalizeText(node.GetAttributeValue("href", null)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Select(value => ToAbsoluteUri(pageUri, value))
            .Where(uri => uri is not null)
            .Select(uri => NormalizeUri(uri!))
            .Where(uri => IsProductUrl(definition, uri))
            .Select(uri => uri.AbsoluteUri)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
    }

    private static IReadOnlyCollection<Uri> ExtractDiscoveryPages(
        HtmlDocument document,
        Uri pageUri,
        RetailerDefinition definition)
    {
        return document.DocumentNode
            .SelectNodes("//a[@href]")
            ?.Select(node => HtmlHelpers.NormalizeText(node.GetAttributeValue("href", null)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Select(value => ToAbsoluteUri(pageUri, value))
            .Where(uri => uri is not null)
            .Select(uri => NormalizeUri(uri!))
            .Where(uri => IsDiscoveryPage(definition, uri))
            .DistinctBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<Uri>();
    }

    private static bool IsProductUrl(RetailerDefinition definition, Uri uri)
    {
        if (!IsSameRetailerHost(definition, uri))
        {
            return false;
        }

        if (HasIgnoredExtension(uri))
        {
            return false;
        }

        return definition.BootstrapProductUrlMarkers.Any(marker =>
            uri.AbsoluteUri.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDiscoveryPage(RetailerDefinition definition, Uri uri)
    {
        if (!IsSameRetailerHost(definition, uri) ||
            IsProductUrl(definition, uri) ||
            HasIgnoredExtension(uri))
        {
            return false;
        }

        if (definition.BootstrapExcludedPathPrefixes.Any(prefix =>
                uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal))
        {
            return true;
        }

        if (definition.BootstrapDiscoveryPathPrefixes.Count == 0)
        {
            return true;
        }

        return definition.BootstrapDiscoveryPathPrefixes.Any(prefix =>
            uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasIgnoredExtension(Uri uri)
    {
        var extension = Path.GetExtension(uri.AbsolutePath);
        return !string.IsNullOrWhiteSpace(extension) &&
               IgnoredExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSameRetailerHost(RetailerDefinition definition, Uri uri)
    {
        return definition.Hosts.Any(host =>
            uri.Host.Contains(host, StringComparison.OrdinalIgnoreCase));
    }

    private static Uri NormalizeUri(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        foreach (var key in query.AllKeys.Where(key =>
                     key is not null &&
                     (key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(key, "gclid", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(key, "fbclid", StringComparison.OrdinalIgnoreCase)))
                     .ToArray())
        {
            query.Remove(key);
        }

        builder.Query = query.ToString() ?? string.Empty;
        builder.Path = string.IsNullOrWhiteSpace(builder.Path) ? "/" : builder.Path.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(builder.Path))
        {
            builder.Path = "/";
        }

        return builder.Uri;
    }

    private static string NormalizePageKey(Uri uri)
    {
        return NormalizeUri(uri).AbsoluteUri;
    }

    private static Uri? ToAbsoluteUri(Uri pageUri, string href)
    {
        if (string.IsNullOrWhiteSpace(href) ||
            href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return Uri.TryCreate(href, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(pageUri, href);
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyCollection<string>> PersistSnapshotAsync(
        RetailerDefinition definition,
        IReadOnlyCollection<string> urls,
        BootstrapSnapshotMetadata metadata,
        CancellationToken cancellationToken)
    {
        const int maxUrlsPerFile = 50000;

        var outputDirectory = ResolveOutputDirectory(definition);
        Directory.CreateDirectory(outputDirectory);

        var parentDirectory = Directory.GetParent(outputDirectory)?.FullName ?? outputDirectory;
        var stagingDirectory = Path.Combine(parentDirectory, $".{definition.Retailer.ToString().ToLowerInvariant()}-bootstrap-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var chunks = urls.Chunk(maxUrlsPerFile).ToArray();
            for (var index = 0; index < chunks.Length; index++)
            {
                var filePath = Path.Combine(stagingDirectory, $"{SnapshotFilePrefix}_{index + 1:000}.xml");
                var document = new XDocument(
                    new XElement(
                        XName.Get("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                        chunks[index].Select(url =>
                            new XElement(
                                XName.Get("url", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                                new XElement(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9"), url)))));

                document.Save(filePath);
            }

            var metadataPath = Path.Combine(stagingDirectory, MetadataFileName);
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata), cancellationToken);

            foreach (var existingFile in Directory.EnumerateFiles(outputDirectory, $"{SnapshotFilePrefix}_*.xml", SearchOption.TopDirectoryOnly))
            {
                File.Delete(existingFile);
            }

            var existingMetadataPath = Path.Combine(outputDirectory, MetadataFileName);
            if (File.Exists(existingMetadataPath))
            {
                File.Delete(existingMetadataPath);
            }

            var persistedFiles = new List<string>();
            foreach (var stagedFile in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                var destination = Path.Combine(outputDirectory, Path.GetFileName(stagedFile));
                File.Move(stagedFile, destination, overwrite: true);
                if (destination.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    persistedFiles.Add(destination);
                }
            }

            return persistedFiles;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private IReadOnlyCollection<string> ResolvePersistedSitemapFiles(RetailerDefinition definition)
    {
        var outputDirectory = ResolveOutputDirectory(definition);
        if (!Directory.Exists(outputDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(outputDirectory, $"{SnapshotFilePrefix}_*.xml", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string? ResolveFreshnessMarkerPath(RetailerDefinition definition)
    {
        var metadataPath = Path.Combine(ResolveOutputDirectory(definition), MetadataFileName);
        if (File.Exists(metadataPath))
        {
            return metadataPath;
        }

        return ResolvePersistedSitemapFiles(definition)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private async Task<BootstrapSnapshotMetadata?> TryReadMetadataAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(ResolveOutputDirectory(definition), MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        return JsonSerializer.Deserialize<BootstrapSnapshotMetadata>(content);
    }

    private static IReadOnlyCollection<string> ParseLocations(string xml)
    {
        var document = XDocument.Parse(xml);
        return document.Descendants()
            .Where(node => node.Name.LocalName == "loc")
            .Select(node => node.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private async Task DelayBeforeRequestAsync(CancellationToken cancellationToken)
    {
        var minDelay = Math.Max(0, _options.RequestMinDelayMs);
        var maxDelay = Math.Max(minDelay, _options.RequestMaxDelayMs);
        var delay = Random.Shared.Next(minDelay, maxDelay + 1);
        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    private async Task DelayForRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var baseDelay = Math.Max(0, _options.RetryBaseDelayMs);
        var jitterMax = Math.Max(0, _options.RetryJitterMaxMs);
        var delay = (int)Math.Min(int.MaxValue, (baseDelay * Math.Pow(2, Math.Max(0, attempt - 1))) + Random.Shared.Next(0, jitterMax + 1));
        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool IsRetryable(Exception exception)
    {
        return exception is HttpRequestException or TimeoutException or RetryableBootstrapException;
    }

    private static bool LooksLikeChallenge(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return true;
        }

        var normalized = html.ToLowerInvariant();
        return normalized.Contains("captcha", StringComparison.Ordinal) ||
               normalized.Contains("access denied", StringComparison.Ordinal) ||
               normalized.Contains("forbidden", StringComparison.Ordinal) ||
               normalized.Contains("cloudflare", StringComparison.Ordinal) ||
               normalized.Contains("challenge", StringComparison.Ordinal) ||
               normalized.Contains("not a robot", StringComparison.Ordinal);
    }

    private int ResolveMaxPages(RetailerDefinition definition)
    {
        return Math.Max(1, definition.BootstrapMaxPages ?? _options.MaxPages);
    }

    private int ResolveMaxConcurrency(RetailerDefinition definition)
    {
        return Math.Max(1, definition.BootstrapMaxConcurrency ?? _options.MaxConcurrency);
    }

    private sealed record BootstrapPageScanResult(
        IReadOnlyCollection<string> ProductUrls,
        IReadOnlyCollection<Uri> DiscoveryPages);

    private sealed class BootstrapSnapshotMetadata
    {
        public DateTimeOffset RefreshedAtUtc { get; set; }
        public int GeneratedSitemaps { get; set; }
        public int CrawledPages { get; set; }
        public int EnqueuedPages { get; set; }
        public int DiscoveredProducts { get; set; }
        public int PersistedProducts { get; set; }
        public int? RequestedTake { get; set; }
    }

    private sealed class RetryableBootstrapException : Exception
    {
        public RetryableBootstrapException(string message)
            : base(message)
        {
        }
    }
}
