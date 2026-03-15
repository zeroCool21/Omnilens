using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Collections.Concurrent;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class AmazonCatalogBootstrapService
{
    private static readonly Regex AmazonProductUrlRegex = new("(?:/dp/|/gp/product/)(?<asin>[A-Z0-9]{10})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly PageContentService _pageContentService;
    private readonly AmazonCatalogOptions _options;

    public AmazonCatalogBootstrapService(
        PageContentService pageContentService,
        IOptions<AmazonCatalogOptions> options)
    {
        _pageContentService = pageContentService;
        _options = options.Value;
    }

    public async Task<AmazonCatalogBootstrapResponse> BootstrapAsync(bool force, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new ConcurrentQueue<string>();
        var crawledPages = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var productUrls = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var scheduledPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Uri>();

        foreach (var seed in GetSeedUrls())
        {
            var seedKey = NormalizePageKey(seed);
            if (scheduledPages.Add(seedKey))
            {
                queue.Enqueue(seed);
            }
        }

        var outputDirectory = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        if (force)
        {
            foreach (var existingFile in Directory.EnumerateFiles(outputDirectory, $"{_options.BootstrapFilePrefix}_*.xml"))
            {
                File.Delete(existingFile);
            }
        }

        while (queue.Count > 0 && crawledPages.Count < _options.BootstrapMaxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remainingCapacity = _options.BootstrapMaxPages - crawledPages.Count;
            var batchSize = Math.Min(
                Math.Max(1, _options.BootstrapMaxConcurrency),
                Math.Min(queue.Count, remainingCapacity));

            var batch = new List<Uri>(batchSize);
            while (batch.Count < batchSize && queue.Count > 0)
            {
                batch.Add(queue.Dequeue());
            }

            if (batch.Count == 0)
            {
                break;
            }

            var discoveredPages = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, _options.BootstrapMaxConcurrency),
                    CancellationToken = cancellationToken
                },
                async (page, ct) =>
                {
                    var pageKey = NormalizePageKey(page);
                    crawledPages.TryAdd(pageKey, 0);

                    try
                    {
                        var html = await _pageContentService.GetHtmlAsync(page, _options.BootstrapUseBrowser, ct);
                        var document = HtmlHelpers.Load(html);

                        foreach (var productUrl in ExtractProductUrls(document, page))
                        {
                            productUrls.TryAdd(productUrl, 0);
                        }

                        foreach (var nextCategoryPage in ExtractCategoryPages(document, page))
                        {
                            var nextKey = NormalizePageKey(nextCategoryPage);
                            discoveredPages.TryAdd(nextKey, nextCategoryPage);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        warnings.Enqueue($"{page}: {exception.Message}");
                    }
                });

            foreach (var discoveredPage in discoveredPages.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (scheduledPages.Count >= _options.BootstrapMaxPages)
                {
                    break;
                }

                if (scheduledPages.Add(discoveredPage.Key))
                {
                    queue.Enqueue(discoveredPage.Value);
                }
            }
        }

        if (queue.Count > 0)
        {
            warnings.Enqueue($"Bootstrap interrotto al limite di {_options.BootstrapMaxPages} pagine. Aumenta AmazonCatalog.BootstrapMaxPages se vuoi esplorare piu a fondo.");
        }

        var sitemapFiles = WriteProductSitemaps(outputDirectory, productUrls.Keys.ToArray(), _options.BootstrapFilePrefix);
        stopwatch.Stop();

        return new AmazonCatalogBootstrapResponse
        {
            Success = productUrls.Count > 0,
            OutputDirectory = outputDirectory,
            CrawledPages = crawledPages.Count,
            EnqueuedPages = scheduledPages.Count,
            GeneratedSitemaps = sitemapFiles.Count,
            DiscoveredProducts = productUrls.Count,
            DurationMs = stopwatch.ElapsedMilliseconds,
            SitemapFiles = sitemapFiles,
            Warnings = warnings.ToArray()
        };
    }

    public string ResolveOutputDirectory()
    {
        var configured = string.IsNullOrWhiteSpace(_options.CatalogDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "AmazonCatalog", "bootstrap")
            : _options.CatalogDirectory;

        return Path.GetFullPath(configured);
    }

    private IReadOnlyCollection<Uri> GetSeedUrls()
    {
        var rawSeeds = _options.BootstrapSeedUrls.Count > 0
            ? _options.BootstrapSeedUrls
            : new List<string> { "https://www.amazon.it/gp/bestsellers/" };

        return rawSeeds
            .Where(seed => !string.IsNullOrWhiteSpace(seed))
            .Select(seed => Uri.TryCreate(seed, UriKind.Absolute, out var uri)
                ? uri
                : new Uri(new Uri("https://www.amazon.it"), seed))
            .DistinctBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<string> WriteProductSitemaps(
        string outputDirectory,
        IReadOnlyCollection<string> productUrls,
        string filePrefix)
    {
        const int maxUrlsPerSitemap = 50000;

        var files = new List<string>();
        var chunks = productUrls
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .Chunk(maxUrlsPerSitemap)
            .ToArray();

        for (var index = 0; index < chunks.Length; index++)
        {
            var filePath = Path.Combine(outputDirectory, $"{filePrefix}_{index + 1:000}.xml");
            var document = new XDocument(
                new XElement(
                    XName.Get("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                    chunks[index].Select(url =>
                        new XElement(XName.Get("url", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                            new XElement(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9"), url)))));

            document.Save(filePath);
            files.Add(filePath);
        }

        return files;
    }

    private static IReadOnlyCollection<string> ExtractProductUrls(HtmlDocument document, Uri page)
    {
        return document.DocumentNode
            .SelectNodes("//a[@href]")
            ?.Select(node => HtmlHelpers.NormalizeText(node.GetAttributeValue("href", null)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Select(value => ToAbsoluteUri(page, value!))
            .Where(uri => uri is not null)
            .Select(uri => CanonicalizeProductUrl(uri!))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
    }

    private static IReadOnlyCollection<Uri> ExtractCategoryPages(HtmlDocument document, Uri page)
    {
        return document.DocumentNode
            .SelectNodes("//a[@href]")
            ?.Select(node => HtmlHelpers.NormalizeText(node.GetAttributeValue("href", null)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => ToAbsoluteUri(page, value!))
            .Where(uri => uri is not null && IsSupportedAmazonCategoryPage(uri))
            .Select(uri => NormalizeCategoryUri(uri!))
            .DistinctBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<Uri>();
    }

    private static Uri? ToAbsoluteUri(Uri page, string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return Uri.TryCreate(href, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(page, href);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSupportedAmazonCategoryPage(Uri uri)
    {
        if (!uri.Host.Contains("amazon.it", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.AbsolutePath.Contains("/gp/bestsellers/", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri NormalizeCategoryUri(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        var path = builder.Path;
        var refIndex = path.IndexOf("/ref=", StringComparison.OrdinalIgnoreCase);
        if (refIndex >= 0)
        {
            path = path[..refIndex];
        }

        builder.Path = path.TrimEnd('/');

        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        var page = query["pg"];
        query.Clear();
        if (!string.IsNullOrWhiteSpace(page))
        {
            query["pg"] = page;
        }

        builder.Query = query.ToString() ?? string.Empty;
        return builder.Uri;
    }

    private static string NormalizePageKey(Uri uri)
    {
        return NormalizeCategoryUri(uri).AbsoluteUri;
    }

    private static string? CanonicalizeProductUrl(Uri uri)
    {
        if (!uri.Host.Contains("amazon.it", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var match = AmazonProductUrlRegex.Match(uri.AbsoluteUri);
        return match.Success
            ? $"https://www.amazon.it/dp/{match.Groups["asin"].Value.ToUpperInvariant()}"
            : null;
    }
}
