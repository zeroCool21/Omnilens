using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class CatalogDiscoveryService
{
    private readonly IEnumerable<ICatalogUrlSource> _sources;
    private readonly RetailerRegistry _retailerRegistry;

    public CatalogDiscoveryService(IEnumerable<ICatalogUrlSource> sources, RetailerRegistry retailerRegistry)
    {
        _sources = sources;
        _retailerRegistry = retailerRegistry;
    }

    public Task<IReadOnlyCollection<string>> GetSampleProductUrlsAsync(
        RetailerType retailer,
        int take,
        CancellationToken cancellationToken)
    {
        return ResolveSource(retailer).GetSampleProductUrlsAsync(_retailerRegistry.Get(retailer), take, cancellationToken);
    }

    public Task<int> CountProductUrlsAsync(
        RetailerType retailer,
        CancellationToken cancellationToken)
    {
        return ResolveSource(retailer).CountProductUrlsAsync(_retailerRegistry.Get(retailer), cancellationToken);
    }

    public Task<int> CountProductSourcesAsync(
        RetailerType retailer,
        CancellationToken cancellationToken)
    {
        return ResolveSource(retailer).CountProductSourcesAsync(_retailerRegistry.Get(retailer), cancellationToken);
    }

    private ICatalogUrlSource ResolveSource(RetailerType retailer)
    {
        var definition = _retailerRegistry.Get(retailer);
        var source = _sources.FirstOrDefault(candidate => candidate.CanHandle(definition));
        if (source is not null)
        {
            return source;
        }

        throw new NotSupportedException(
            definition.CatalogNotes ??
            $"Non esiste una sorgente catalogo registrata per {definition.DisplayName}.");
    }
}
