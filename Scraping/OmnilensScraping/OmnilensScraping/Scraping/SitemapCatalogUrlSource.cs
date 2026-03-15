using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace OmnilensScraping.Scraping;

public class SitemapCatalogUrlSource : ICatalogUrlSource
{
    private readonly PageContentService _pageContentService;
    private readonly ScrapingOptions _options;

    public SitemapCatalogUrlSource(
        PageContentService pageContentService,
        IOptions<ScrapingOptions> options)
    {
        _pageContentService = pageContentService;
        _options = options.Value;
    }

    public bool CanHandle(RetailerDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.SitemapIndexUrl) &&
               definition.ProductSitemapMarkers.Count > 0;
    }

    public async Task<IReadOnlyCollection<string>> GetSampleProductUrlsAsync(
        RetailerDefinition definition,
        int take,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return Array.Empty<string>();
        }

        var productSitemaps = await GetProductSitemapUrlsAsync(definition, cancellationToken);
        var results = new List<string>();

        foreach (var sitemapUrl in productSitemaps)
        {
            var content = await ReadSitemapAsync(new Uri(sitemapUrl), cancellationToken);
            foreach (var location in ParseLocations(content))
            {
                results.Add(location);
                if (results.Count >= take)
                {
                    return results;
                }
            }
        }

        return results;
    }

    public async Task<int> CountProductUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var productSitemaps = await GetProductSitemapUrlsAsync(definition, cancellationToken);
        var total = 0;

        await Parallel.ForEachAsync(
            productSitemaps,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, _options.MaxSitemapConcurrency),
                CancellationToken = cancellationToken
            },
            async (sitemapUrl, ct) =>
            {
                var content = await ReadSitemapAsync(new Uri(sitemapUrl), ct);
                var count = ParseLocations(content).Count;
                Interlocked.Add(ref total, count);
            });

        return total;
    }

    public async Task<int> CountProductSourcesAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var productSitemaps = await GetProductSitemapUrlsAsync(definition, cancellationToken);
        return productSitemaps.Count;
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

    private static IReadOnlyCollection<string> ParseLocations(string xml)
    {
        var document = XDocument.Parse(xml);
        return document.Descendants()
            .Where(node => node.Name.LocalName == "loc")
            .Select(node => node.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}
