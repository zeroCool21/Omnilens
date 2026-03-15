using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class AmazonCatalogBootstrapService
{
    private static readonly Regex AmazonProductUrlRegex = new("(?:/dp/|/gp/product/)(?<asin>[A-Z0-9]{10})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SlashRegex = new("[\\\\/]+", RegexOptions.Compiled);
    private static readonly Regex SlugNoiseRegex = new("[^a-z0-9]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UrlNoiseRegex = new("^[0-9]+$|^[A-Z0-9]{8,}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] BreadcrumbXPaths =
    {
        "//*[@id='wayfinding-breadcrumbs_feature_div']//a",
        "//*[@id='wayfinding-breadcrumbs_container']//a",
        "//*[@data-component-type='s-breadcrumb']//a",
        "//*[@id='zg_browseRoot']//a",
        "//*[contains(@class,'breadcrumb')]//a"
    };

    private static readonly string[] DefaultSeedUrls =
    {
        "https://www.amazon.it/",
        "https://www.amazon.it/gp/site-directory",
        "https://www.amazon.it/gp/bestsellers/",
        "https://www.amazon.it/gp/new-releases/",
        "https://www.amazon.it/gp/most-wished-for/",
        "https://www.amazon.it/gp/movers-and-shakers/",
        "https://www.amazon.it/deals"
    };

    private static readonly string[] DefaultSearchAliases =
    {
        "electronics",
        "computers",
        "appliances",
        "kitchen",
        "home",
        "diy",
        "tools",
        "garden",
        "lighting",
        "office-products",
        "videogames",
        "toys",
        "fashion",
        "watches",
        "shoes",
        "luggage",
        "sports",
        "automotive",
        "pet-supplies",
        "beauty",
        "drugstore",
        "grocery",
        "baby",
        "books",
        "dvd",
        "music"
    };

    private static readonly HashSet<string> IgnoredCategoryLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "amazon",
        "amazon.it",
        "categorie",
        "categoria",
        "tutte le categorie",
        "negozio",
        "store",
        "offerte",
        "deals",
        "pagina successiva",
        "pagina precedente",
        "next page",
        "previous page",
        "more results",
        "results",
        "prime",
        "nuovi arrivi",
        "piu venduti",
        "best sellers",
        "best seller",
        "movers and shakers",
        "most wished for"
    };

    private static readonly string[] UnsupportedPathPrefixes =
    {
        "/ap/",
        "/gp/aw/",
        "/gp/cart",
        "/gp/css",
        "/gp/help",
        "/gp/video",
        "/gp/your-account",
        "/hz/",
        "/wishlist",
        "/registry",
        "/business",
        "/music/player"
    };

    private static readonly string[] DiscoveryPathPrefixes =
    {
        "/s",
        "/b",
        "/stores",
        "/deals",
        "/gp/browse",
        "/gp/site-directory",
        "/gp/bestsellers",
        "/gp/new-releases",
        "/gp/most-wished-for",
        "/gp/movers-and-shakers"
    };

    private static readonly string[] AllowedDiscoveryQueryKeys =
    {
        "i",
        "rh",
        "node",
        "bbn",
        "page",
        "pg"
    };

    private readonly PageContentService _pageContentService;
    private readonly AmazonCatalogOptions _options;

    public AmazonCatalogBootstrapService(
        PageContentService pageContentService,
        IOptions<AmazonCatalogOptions> options)
    {
        _pageContentService = pageContentService;
        _options = options.Value;
    }

    public async Task<AmazonCatalogBootstrapResponse> BootstrapAsync(
        bool force,
        int take = 0,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestedTake = take > 0 ? take : 0;
        var warnings = new ConcurrentQueue<string>();
        var crawledPages = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var productUrls = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var categoryMap = new ConcurrentDictionary<string, AmazonCategoryContext>(StringComparer.OrdinalIgnoreCase);
        var productCategories = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>(StringComparer.OrdinalIgnoreCase);
        var scheduledPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<AmazonCatalogPage>();

        foreach (var seed in GetSeedPages())
        {
            var seedKey = NormalizePageKey(seed.PageUri);
            if (scheduledPages.Add(seedKey))
            {
                queue.Enqueue(seed);
            }
        }

        categoryMap.TryAdd(AmazonCategoryContext.Uncategorized.Key, AmazonCategoryContext.Uncategorized);

        var outputDirectory = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        while (queue.Count > 0 && crawledPages.Count < _options.BootstrapMaxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remainingCapacity = _options.BootstrapMaxPages - crawledPages.Count;
            var batchSize = Math.Min(
                Math.Max(1, _options.BootstrapMaxConcurrency),
                Math.Min(queue.Count, remainingCapacity));

            var batch = new List<AmazonCatalogPage>(batchSize);
            while (batch.Count < batchSize && queue.Count > 0)
            {
                batch.Add(queue.Dequeue());
            }

            if (batch.Count == 0)
            {
                break;
            }

            var discoveredPages = new ConcurrentDictionary<string, AmazonCatalogPage>(StringComparer.OrdinalIgnoreCase);

            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, _options.BootstrapMaxConcurrency),
                    CancellationToken = cancellationToken
                },
                async (page, ct) =>
                {
                    var pageKey = NormalizePageKey(page.PageUri);
                    crawledPages.TryAdd(pageKey, 0);

                    try
                    {
                        var html = await _pageContentService.GetHtmlAsync(page.PageUri, _options.BootstrapUseBrowser, ct);
                        var document = HtmlHelpers.Load(html);
                        var resolvedCategory = ResolvePageCategory(document, page.PageUri, page.Category);
                        categoryMap.TryAdd(resolvedCategory.Key, resolvedCategory);

                        foreach (var productUrl in ExtractProductUrls(document, page.PageUri))
                        {
                            productUrls.TryAdd(productUrl, 0);
                            AddProductCategory(productCategories, productUrl, resolvedCategory);
                        }

                        foreach (var nextPage in ExtractDiscoveryPages(document, page.PageUri, resolvedCategory))
                        {
                            var nextKey = NormalizePageKey(nextPage.PageUri);
                            discoveredPages.AddOrUpdate(
                                nextKey,
                                static (_, candidate) => candidate,
                                static (_, existing, candidate) => ChooseMoreSpecificPage(existing, candidate),
                                nextPage);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        warnings.Enqueue($"{page.PageUri}: {exception.Message}");
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

            if (requestedTake > 0 && productUrls.Count >= requestedTake)
            {
                warnings.Enqueue($"Raggiunto il limite take={requestedTake}. La snapshot verra salvata con al massimo {requestedTake} prodotti.");
                break;
            }
        }

        if (queue.Count > 0)
        {
            warnings.Enqueue($"Bootstrap interrotto al limite di {_options.BootstrapMaxPages} pagine. Aumenta AmazonCatalog.BootstrapMaxPages se vuoi esplorare piu a fondo.");
        }

        var persistedProductUrls = productUrls.Keys
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .Take(requestedTake > 0 ? requestedTake : int.MaxValue)
            .ToArray();

        var categoryBuckets = BuildCategoryBuckets(persistedProductUrls, productCategories, categoryMap);
        var sitemapFiles = force || persistedProductUrls.Length > 0
            ? PersistCategorySitemaps(outputDirectory, categoryBuckets, _options.BootstrapFilePrefix)
            : Array.Empty<string>();

        stopwatch.Stop();

        return new AmazonCatalogBootstrapResponse
        {
            Success = persistedProductUrls.Length > 0,
            OutputDirectory = outputDirectory,
            CrawledPages = crawledPages.Count,
            EnqueuedPages = scheduledPages.Count,
            GeneratedSitemaps = sitemapFiles.Count,
            DiscoveredProducts = productUrls.Count,
            PersistedProducts = persistedProductUrls.Length,
            DiscoveredCategories = categoryBuckets.Count(bucket => !bucket.Category.IsUncategorized),
            RequestedTake = requestedTake > 0 ? requestedTake : null,
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

    private IReadOnlyCollection<AmazonCatalogPage> GetSeedPages()
    {
        IEnumerable<string> seeds = _options.BootstrapSeedUrls.Count > 0
            ? _options.BootstrapSeedUrls
            : DefaultSeedUrls;

        var results = new List<AmazonCatalogPage>();
        foreach (var seed in seeds)
        {
            var uri = Uri.TryCreate(seed, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(new Uri("https://www.amazon.it"), seed);

            results.Add(new AmazonCatalogPage(NormalizeDiscoveryUri(uri), ExtractSeedCategory(uri)));
        }

        IEnumerable<string> aliases = _options.BootstrapSearchAliases.Count > 0
            ? _options.BootstrapSearchAliases
            : DefaultSearchAliases;

        foreach (var alias in aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)))
        {
            var uri = new Uri($"https://www.amazon.it/s?i={Uri.EscapeDataString(alias)}");
            var formattedAlias = FormatUrlToken(alias);
            results.Add(new AmazonCatalogPage(
                uri,
                string.IsNullOrWhiteSpace(formattedAlias)
                    ? AmazonCategoryContext.Uncategorized
                    : CreateCategoryContext(new[] { formattedAlias })));
        }

        return results
            .DistinctBy(page => NormalizePageKey(page.PageUri), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AmazonCategoryContext ExtractSeedCategory(Uri uri)
    {
        var fromUrl = ExtractUrlCategorySegments(uri);
        return fromUrl.Count > 0 ? CreateCategoryContext(fromUrl) : AmazonCategoryContext.Uncategorized;
    }

    private static IReadOnlyCollection<AmazonCategoryBucket> BuildCategoryBuckets(
        IReadOnlyCollection<string> productUrls,
        ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> productCategories,
        ConcurrentDictionary<string, AmazonCategoryContext> categoryMap)
    {
        var buckets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        categoryMap.TryAdd(AmazonCategoryContext.Uncategorized.Key, AmazonCategoryContext.Uncategorized);

        foreach (var productUrl in productUrls.OrderBy(url => url, StringComparer.OrdinalIgnoreCase))
        {
            if (!productCategories.TryGetValue(productUrl, out var categories) || categories.Count == 0)
            {
                GetBucketUrls(buckets, AmazonCategoryContext.Uncategorized.Key).Add(productUrl);
                continue;
            }

            foreach (var categoryKey in categories.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                GetBucketUrls(buckets, categoryKey).Add(productUrl);
            }
        }

        return buckets
            .Select(entry =>
            {
                var category = categoryMap.TryGetValue(entry.Key, out var resolvedCategory)
                    ? resolvedCategory
                    : AmazonCategoryContext.Uncategorized;

                return new AmazonCategoryBucket(category, entry.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
            })
            .OrderBy(bucket => bucket.Category.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<string> GetBucketUrls(IDictionary<string, List<string>> buckets, string key)
    {
        if (!buckets.TryGetValue(key, out var urls))
        {
            urls = new List<string>();
            buckets[key] = urls;
        }

        return urls;
    }

    private static IReadOnlyCollection<string> PersistCategorySitemaps(
        string outputDirectory,
        IReadOnlyCollection<AmazonCategoryBucket> categoryBuckets,
        string filePrefix)
    {
        var parentDirectory = Directory.GetParent(outputDirectory)?.FullName ?? outputDirectory;
        var stagingDirectory = Path.Combine(parentDirectory, $".{Path.GetFileName(outputDirectory)}-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var stagedFiles = WriteCategorySitemaps(stagingDirectory, categoryBuckets, filePrefix);
            return ReplaceExistingSnapshot(outputDirectory, stagingDirectory, stagedFiles, filePrefix);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static IReadOnlyCollection<string> ReplaceExistingSnapshot(
        string outputDirectory,
        string stagingDirectory,
        IReadOnlyCollection<string> stagedFiles,
        string filePrefix)
    {
        foreach (var existingFile in Directory.EnumerateFiles(outputDirectory, $"{filePrefix}_*.xml", SearchOption.AllDirectories))
        {
            File.Delete(existingFile);
        }

        RemoveEmptyDirectories(outputDirectory);

        var persistedFiles = new List<string>(stagedFiles.Count);
        foreach (var stagedFile in stagedFiles)
        {
            var relativePath = Path.GetRelativePath(stagingDirectory, stagedFile);
            var destinationPath = Path.GetFullPath(Path.Combine(outputDirectory, relativePath));
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Move(stagedFile, destinationPath, overwrite: true);
            persistedFiles.Add(destinationPath);
        }

        RemoveEmptyDirectories(outputDirectory);
        return persistedFiles;
    }

    private static void RemoveEmptyDirectories(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }

    private static IReadOnlyCollection<string> WriteCategorySitemaps(
        string outputDirectory,
        IReadOnlyCollection<AmazonCategoryBucket> categoryBuckets,
        string filePrefix)
    {
        const int maxUrlsPerSitemap = 50000;

        var files = new List<string>();

        foreach (var bucket in categoryBuckets.Where(bucket => bucket.Urls.Count > 0))
        {
            var categoryDirectory = ResolveCategoryDirectory(outputDirectory, bucket.Category);
            Directory.CreateDirectory(categoryDirectory);

            var chunks = bucket.Urls
                .Chunk(maxUrlsPerSitemap)
                .ToArray();

            for (var index = 0; index < chunks.Length; index++)
            {
                var filePath = Path.Combine(categoryDirectory, $"{filePrefix}_{index + 1:000}.xml");
                var document = new XDocument(
                    new XElement(
                        XName.Get("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                        chunks[index].Select(url =>
                            new XElement(
                                XName.Get("url", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                                new XElement(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9"), url)))));

                document.Save(filePath);
                files.Add(filePath);
            }
        }

        return files;
    }

    private static string ResolveCategoryDirectory(string rootDirectory, AmazonCategoryContext category)
    {
        var segments = category.IsUncategorized
            ? new[] { "_categories", "_uncategorized" }
            : new[] { "_categories" }.Concat(category.Segments.Select(ToSlug));

        return Path.Combine(new[] { rootDirectory }.Concat(segments).ToArray());
    }

    private static AmazonCatalogPage ChooseMoreSpecificPage(AmazonCatalogPage existing, AmazonCatalogPage candidate)
    {
        if (existing.Category.IsUncategorized && !candidate.Category.IsUncategorized)
        {
            return candidate;
        }

        return candidate.Category.Segments.Length > existing.Category.Segments.Length
            ? candidate
            : existing;
    }

    private static void AddProductCategory(
        ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> productCategories,
        string productUrl,
        AmazonCategoryContext category)
    {
        var categorySet = productCategories.GetOrAdd(
            productUrl,
            static _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

        categorySet.TryAdd(category.Key, 0);
    }

    private static IReadOnlyCollection<string> ExtractProductUrls(HtmlDocument document, Uri page)
    {
        return document.DocumentNode
            .SelectNodes("//a[@href]")
            ?.Select(node => HtmlHelpers.NormalizeText(node.GetAttributeValue("href", null)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Select(value => ToAbsoluteUri(page, value))
            .Where(uri => uri is not null)
            .Select(uri => CanonicalizeProductUrl(uri!))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
    }

    private static IReadOnlyCollection<AmazonCatalogPage> ExtractDiscoveryPages(
        HtmlDocument document,
        Uri page,
        AmazonCategoryContext currentCategory)
    {
        return document.DocumentNode
            .SelectNodes("//a[@href]")
            ?.Select(node =>
            {
                var href = HtmlHelpers.NormalizeText(node.GetAttributeValue("href", null));
                if (string.IsNullOrWhiteSpace(href))
                {
                    return null;
                }

                var uri = ToAbsoluteUri(page, href!);
                if (uri is null || !IsSupportedAmazonDiscoveryPage(uri))
                {
                    return null;
                }

                var normalized = NormalizeDiscoveryUri(uri);
                var currentKey = NormalizePageKey(page);
                if (string.Equals(NormalizePageKey(normalized), currentKey, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var anchorText = HtmlHelpers.NormalizeText(node.InnerText);
                var discoveredCategory = BuildDiscoveryCategory(normalized, currentCategory, anchorText);
                return new AmazonCatalogPage(normalized, discoveredCategory);
            })
            .Where(candidate => candidate is not null)
            .Cast<AmazonCatalogPage>()
            .DistinctBy(candidate => NormalizePageKey(candidate.PageUri), StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<AmazonCatalogPage>();
    }

    private static AmazonCategoryContext ResolvePageCategory(
        HtmlDocument document,
        Uri page,
        AmazonCategoryContext fallback)
    {
        var breadcrumbSegments = ExtractBreadcrumbSegments(document);
        if (breadcrumbSegments.Count > 0)
        {
            return CreateCategoryContext(breadcrumbSegments);
        }

        var urlSegments = ExtractUrlCategorySegments(page);
        if (urlSegments.Count > 0)
        {
            return CreateCategoryContext(urlSegments);
        }

        var heading = ExtractHeading(document);
        if (!string.IsNullOrWhiteSpace(heading))
        {
            return fallback.IsUncategorized
                ? CreateCategoryContext(new[] { heading })
                : CreateCategoryContext(fallback.Segments.Concat(new[] { heading }).Take(6));
        }

        return fallback;
    }

    private static IReadOnlyCollection<string> ExtractBreadcrumbSegments(HtmlDocument document)
    {
        var segments = new List<string>();

        foreach (var xpath in BreadcrumbXPaths)
        {
            var nodes = document.DocumentNode.SelectNodes(xpath);
            if (nodes is null)
            {
                continue;
            }

            foreach (var node in nodes)
            {
                var segment = FormatCategoryToken(node.InnerText);
                if (!string.IsNullOrWhiteSpace(segment) &&
                    !segments.Contains(segment, StringComparer.OrdinalIgnoreCase))
                {
                    segments.Add(segment);
                }
            }

            if (segments.Count > 0)
            {
                break;
            }
        }

        return segments;
    }

    private static string? ExtractHeading(HtmlDocument document)
    {
        var candidates = new[]
        {
            HtmlHelpers.GetTextByXPath(document, "//h1"),
            HtmlHelpers.GetMetaContent(document, "og:title"),
            HtmlHelpers.GetMetaContent(document, "title")
        };

        return candidates
            .Select(FormatCategoryToken)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static AmazonCategoryContext BuildDiscoveryCategory(
        Uri uri,
        AmazonCategoryContext currentCategory,
        string? anchorText)
    {
        var urlSegments = ExtractUrlCategorySegments(uri);
        if (urlSegments.Count > 0)
        {
            return CreateCategoryContext(urlSegments);
        }

        var anchorSegment = FormatCategoryToken(anchorText);
        if (string.IsNullOrWhiteSpace(anchorSegment))
        {
            return currentCategory;
        }

        if (currentCategory.IsUncategorized)
        {
            return CreateCategoryContext(new[] { anchorSegment });
        }

        if (string.Equals(currentCategory.Segments.LastOrDefault(), anchorSegment, StringComparison.OrdinalIgnoreCase))
        {
            return currentCategory;
        }

        return CreateCategoryContext(currentCategory.Segments.Concat(new[] { anchorSegment }).Take(6));
    }

    private static IReadOnlyCollection<string> ExtractUrlCategorySegments(Uri uri)
    {
        var path = uri.AbsolutePath;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return Array.Empty<string>();
        }

        if (segments[0].Equals("s", StringComparison.OrdinalIgnoreCase))
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var alias = FormatUrlToken(query["i"]);
            return string.IsNullOrWhiteSpace(alias) ? Array.Empty<string>() : new[] { alias };
        }

        if (segments[0].Equals("gp", StringComparison.OrdinalIgnoreCase) && segments.Length >= 2)
        {
            var section = segments[1].ToLowerInvariant();
            if (section is "bestsellers" or "new-releases" or "most-wished-for" or "movers-and-shakers")
            {
                return segments
                    .Skip(2)
                    .Select(FormatUrlToken)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Cast<string>()
                    .ToArray();
            }

            if (section.StartsWith("browse", StringComparison.OrdinalIgnoreCase))
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var node = FormatUrlToken(query["node"]);
                return string.IsNullOrWhiteSpace(node) ? Array.Empty<string>() : new[] { node };
            }
        }

        if (segments[0].Equals("stores", StringComparison.OrdinalIgnoreCase))
        {
            return segments
                .Skip(1)
                .Select(FormatUrlToken)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray();
        }

        return segments
            .Select(FormatUrlToken)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Take(4)
            .ToArray();
    }

    private static AmazonCategoryContext CreateCategoryContext(IEnumerable<string> segments)
    {
        var normalizedSegments = segments
            .Select(FormatCategoryToken)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        return normalizedSegments.Length == 0
            ? AmazonCategoryContext.Uncategorized
            : new AmazonCategoryContext(normalizedSegments);
    }

    private static string? FormatCategoryToken(string? value)
    {
        var normalized = HtmlHelpers.NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        normalized = SlashRegex.Replace(normalized, " ");
        normalized = normalized.Replace(">", " ", StringComparison.Ordinal)
            .Replace("›", " ", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal);
        normalized = HtmlHelpers.NormalizeText(normalized);

        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length < 2 ||
            IgnoredCategoryLabels.Contains(normalized) ||
            normalized.All(char.IsDigit))
        {
            return null;
        }

        var lower = normalized.ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);
    }

    private static string? FormatUrlToken(string? value)
    {
        var normalized = HtmlHelpers.NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        normalized = normalized.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized;
        normalized = normalized.Split('?', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? normalized;
        normalized = normalized.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized;
        normalized = normalized.Trim();

        if (string.IsNullOrWhiteSpace(normalized) || UrlNoiseRegex.IsMatch(normalized))
        {
            return null;
        }

        return FormatCategoryToken(normalized);
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

    private static bool IsSupportedAmazonDiscoveryPage(Uri uri)
    {
        if (!uri.Host.Contains("amazon.it", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(CanonicalizeProductUrl(uri)))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        if (UnsupportedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (path.Equals("/", StringComparison.Ordinal))
        {
            return true;
        }

        if (path.StartsWith("/s", StringComparison.OrdinalIgnoreCase))
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return !string.IsNullOrWhiteSpace(query["i"]) ||
                   !string.IsNullOrWhiteSpace(query["rh"]) ||
                   !string.IsNullOrWhiteSpace(query["node"]) ||
                   !string.IsNullOrWhiteSpace(query["bbn"]);
        }

        return DiscoveryPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static Uri NormalizeDiscoveryUri(Uri uri)
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

        builder.Path = string.IsNullOrWhiteSpace(path)
            ? "/"
            : path.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(builder.Path))
        {
            builder.Path = "/";
        }

        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        var normalizedQuery = System.Web.HttpUtility.ParseQueryString(string.Empty);
        foreach (var key in AllowedDiscoveryQueryKeys)
        {
            var value = query[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                normalizedQuery[key == "pg" ? "page" : key] = value;
            }
        }

        builder.Query = normalizedQuery.ToString() ?? string.Empty;
        return builder.Uri;
    }

    private static string NormalizePageKey(Uri uri)
    {
        return NormalizeDiscoveryUri(uri).AbsoluteUri;
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

    private static string ToSlug(string segment)
    {
        var normalized = RemoveDiacritics(segment).ToLowerInvariant();
        normalized = SlugNoiseRegex.Replace(normalized, "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "category" : normalized;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record AmazonCatalogPage(Uri PageUri, AmazonCategoryContext Category);

    private sealed record AmazonCategoryBucket(AmazonCategoryContext Category, IReadOnlyCollection<string> Urls);

    private sealed record AmazonCategoryContext(string[] Segments)
    {
        public static AmazonCategoryContext Uncategorized { get; } = new(Array.Empty<string>());

        public bool IsUncategorized => Segments.Length == 0;

        public string Key => IsUncategorized
            ? "_uncategorized"
            : string.Join("/", Segments.Select(ToSlug));
    }
}
