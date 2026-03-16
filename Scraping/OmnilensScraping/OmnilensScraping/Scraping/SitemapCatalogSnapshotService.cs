using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace OmnilensScraping.Scraping;

public class SitemapCatalogSnapshotService
{
    private const string SnapshotFilePrefix = "catalog-snapshot";
    private const string MetadataFileName = "catalog-snapshot.metadata.json";

    private readonly PageContentService _pageContentService;
    private readonly ScrapingOptions _scrapingOptions;
    private readonly CatalogRefreshOptions _refreshOptions;
    private readonly ConcurrentDictionary<Models.RetailerType, SemaphoreSlim> _refreshLocks = new();

    public SitemapCatalogSnapshotService(
        PageContentService pageContentService,
        IOptions<ScrapingOptions> scrapingOptions,
        IOptions<CatalogRefreshOptions> refreshOptions)
    {
        _pageContentService = pageContentService;
        _scrapingOptions = scrapingOptions.Value;
        _refreshOptions = refreshOptions.Value;
    }

    public async Task<IReadOnlyCollection<string>> GetOrRefreshSnapshotUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        if (IsSnapshotFresh(definition.Retailer))
        {
            var cachedUrls = await ReadSnapshotUrlsAsync(definition.Retailer, cancellationToken);
            if (cachedUrls.Count > 0)
            {
                return cachedUrls;
            }
        }

        await RefreshAsync(definition, cancellationToken);
        return await ReadSnapshotUrlsAsync(definition.Retailer, cancellationToken);
    }

    public async Task<int> GetOrRefreshProductSourceCountAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var metadata = await TryReadMetadataAsync(definition.Retailer, cancellationToken);
        if (metadata is not null && IsSnapshotFresh(definition.Retailer))
        {
            return metadata.ProductSourceCount;
        }

        await RefreshAsync(definition, cancellationToken);
        metadata = await TryReadMetadataAsync(definition.Retailer, cancellationToken);
        return metadata?.ProductSourceCount ?? 0;
    }

    public async Task RefreshAsync(RetailerDefinition definition, CancellationToken cancellationToken)
    {
        var refreshLock = _refreshLocks.GetOrAdd(
            definition.Retailer,
            static _ => new SemaphoreSlim(1, 1));

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            var sitemapUrls = await GetProductSitemapUrlsAsync(definition, cancellationToken);
            var collectedUrls = new ConcurrentBag<string>();

            await Parallel.ForEachAsync(
                sitemapUrls,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, _scrapingOptions.MaxSitemapConcurrency),
                    CancellationToken = cancellationToken
                },
                async (sitemapUrl, ct) =>
                {
                    var content = await ReadSitemapAsync(new Uri(sitemapUrl), ct);
                    foreach (var location in ParseLocations(content))
                    {
                        collectedUrls.Add(location);
                    }
                });

            var distinctUrls = collectedUrls
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await PersistSnapshotAsync(
                definition.Retailer,
                distinctUrls,
                sitemapUrls.Count,
                cancellationToken);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private bool IsSnapshotFresh(Models.RetailerType retailer)
    {
        var metadataPath = ResolveMetadataPath(retailer);
        if (!File.Exists(metadataPath))
        {
            return false;
        }

        var staleAfter = Math.Max(1, _refreshOptions.SnapshotStaleAfterMinutes);
        var lastWriteUtc = File.GetLastWriteTimeUtc(metadataPath);
        return DateTime.UtcNow - lastWriteUtc < TimeSpan.FromMinutes(staleAfter);
    }

    private async Task<IReadOnlyCollection<string>> ReadSnapshotUrlsAsync(
        Models.RetailerType retailer,
        CancellationToken cancellationToken)
    {
        var snapshotDirectory = ResolveSnapshotDirectory(retailer);
        if (!Directory.Exists(snapshotDirectory))
        {
            return Array.Empty<string>();
        }

        var files = Directory.EnumerateFiles(snapshotDirectory, $"{SnapshotFilePrefix}_*.xml", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

    private async Task<SitemapSnapshotMetadata?> TryReadMetadataAsync(
        Models.RetailerType retailer,
        CancellationToken cancellationToken)
    {
        var metadataPath = ResolveMetadataPath(retailer);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        return JsonSerializer.Deserialize<SitemapSnapshotMetadata>(content);
    }

    private async Task<IReadOnlyCollection<string>> GetProductSitemapUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var sitemapIndex = await _pageContentService.GetStringAsync(new Uri(definition.SitemapIndexUrl), cancellationToken);

        return ParseLocations(sitemapIndex)
            .Where(location => definition.ProductSitemapMarkers.Any(marker =>
                location.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private async Task<string> ReadSitemapAsync(Uri url, CancellationToken cancellationToken)
    {
        if (url.AbsolutePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await _pageContentService.GetBytesAsync(url, cancellationToken);
            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return await reader.ReadToEndAsync().WaitAsync(cancellationToken);
        }

        return await _pageContentService.GetStringAsync(url, cancellationToken);
    }

    private async Task PersistSnapshotAsync(
        Models.RetailerType retailer,
        IReadOnlyCollection<string> urls,
        int productSourceCount,
        CancellationToken cancellationToken)
    {
        const int maxUrlsPerFile = 50000;

        var snapshotDirectory = ResolveSnapshotDirectory(retailer);
        Directory.CreateDirectory(snapshotDirectory);

        var parentDirectory = Directory.GetParent(snapshotDirectory)?.FullName ?? snapshotDirectory;
        var stagingDirectory = Path.Combine(parentDirectory, $".{retailer.ToString().ToLowerInvariant()}-snapshot-staging-{Guid.NewGuid():N}");
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

            var metadata = new SitemapSnapshotMetadata
            {
                ProductSourceCount = productSourceCount,
                TotalProducts = urls.Count,
                RefreshedAtUtc = DateTimeOffset.UtcNow
            };

            var metadataPath = Path.Combine(stagingDirectory, MetadataFileName);
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata), cancellationToken);

            foreach (var existingFile in Directory.EnumerateFiles(snapshotDirectory, $"{SnapshotFilePrefix}_*.xml", SearchOption.TopDirectoryOnly))
            {
                File.Delete(existingFile);
            }

            var existingMetadataPath = ResolveMetadataPath(retailer);
            if (File.Exists(existingMetadataPath))
            {
                File.Delete(existingMetadataPath);
            }

            foreach (var stagedFile in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                var destination = Path.Combine(snapshotDirectory, Path.GetFileName(stagedFile));
                File.Move(stagedFile, destination, overwrite: true);
            }
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private string ResolveSnapshotDirectory(Models.RetailerType retailer)
    {
        var root = string.IsNullOrWhiteSpace(_refreshOptions.SnapshotRootDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "CatalogSnapshots")
            : _refreshOptions.SnapshotRootDirectory;

        return Path.Combine(Path.GetFullPath(root), retailer.ToString());
    }

    private string ResolveMetadataPath(Models.RetailerType retailer)
    {
        return Path.Combine(ResolveSnapshotDirectory(retailer), MetadataFileName);
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

    private sealed class SitemapSnapshotMetadata
    {
        public int ProductSourceCount { get; set; }
        public int TotalProducts { get; set; }
        public DateTimeOffset RefreshedAtUtc { get; set; }
    }
}
