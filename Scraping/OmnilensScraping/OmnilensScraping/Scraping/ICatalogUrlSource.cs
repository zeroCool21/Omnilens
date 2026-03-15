using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public interface ICatalogUrlSource
{
    bool CanHandle(RetailerDefinition definition);
    CatalogCoverageStatus DescribeCoverage(RetailerDefinition definition);
    Task<IReadOnlyCollection<string>> GetSampleProductUrlsAsync(RetailerDefinition definition, int take, CancellationToken cancellationToken);
    Task<int> CountProductUrlsAsync(RetailerDefinition definition, CancellationToken cancellationToken);
    Task<int> CountProductSourcesAsync(RetailerDefinition definition, CancellationToken cancellationToken);
}
