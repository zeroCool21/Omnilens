using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class BootstrapCatalogUrlSource : ICatalogUrlSource
{
    private readonly RetailerCatalogBootstrapService _bootstrapService;

    public BootstrapCatalogUrlSource(RetailerCatalogBootstrapService bootstrapService)
    {
        _bootstrapService = bootstrapService;
    }

    public bool CanHandle(RetailerDefinition definition)
    {
        return _bootstrapService.CanBootstrap(definition);
    }

    public CatalogCoverageStatus DescribeCoverage(RetailerDefinition definition)
    {
        return new CatalogCoverageStatus
        {
            SourceKind = "PublicCrawlSnapshot",
            IsGuaranteedComplete = false,
            Notes = definition.CatalogNotes ??
                    "Catalogo ottenuto da crawl pubblico e snapshot locale: copertura ampia ma non garantita completa."
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

        return (await _bootstrapService.GetOrRefreshSnapshotUrlsAsync(definition, cancellationToken))
            .Take(take)
            .ToArray();
    }

    public async Task<int> CountProductUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        return (await _bootstrapService.GetOrRefreshSnapshotUrlsAsync(definition, cancellationToken)).Count;
    }

    public Task<int> CountProductSourcesAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        return _bootstrapService.GetOrRefreshProductSourceCountAsync(definition, cancellationToken);
    }
}
