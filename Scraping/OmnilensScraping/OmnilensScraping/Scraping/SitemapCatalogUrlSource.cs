using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class SitemapCatalogUrlSource : ICatalogUrlSource
{
    private readonly SitemapCatalogSnapshotService _snapshotService;

    public SitemapCatalogUrlSource(SitemapCatalogSnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    public bool CanHandle(RetailerDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.SitemapIndexUrl) &&
               definition.ProductSitemapMarkers.Count > 0;
    }

    public CatalogCoverageStatus DescribeCoverage(RetailerDefinition definition)
    {
        return new CatalogCoverageStatus
        {
            SourceKind = "PublicSitemap",
            IsGuaranteedComplete = true,
            Notes = "Catalogo letto dai product sitemap pubblici del retailer e mantenuto in snapshot locale aggiornata."
        };
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

        return (await _snapshotService.GetOrRefreshSnapshotUrlsAsync(definition, cancellationToken))
            .Take(take)
            .ToArray();
    }

    public async Task<int> CountProductUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        return (await _snapshotService.GetOrRefreshSnapshotUrlsAsync(definition, cancellationToken)).Count;
    }

    public Task<int> CountProductSourcesAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        return _snapshotService.GetOrRefreshProductSourceCountAsync(definition, cancellationToken);
    }
}
