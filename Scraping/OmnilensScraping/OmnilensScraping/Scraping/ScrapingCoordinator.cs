using System.Diagnostics;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;

namespace OmnilensScraping.Scraping;

public class ScrapingCoordinator
{
    private readonly IEnumerable<IRetailerScraper> _scrapers;
    private readonly RetailerRegistry _retailerRegistry;
    private readonly CatalogDiscoveryService _catalogDiscoveryService;
    private readonly CatalogPersistenceService _catalogPersistenceService;
    private readonly SourceRunTrackingService _sourceRunTrackingService;

    public ScrapingCoordinator(
        IEnumerable<IRetailerScraper> scrapers,
        RetailerRegistry retailerRegistry,
        CatalogDiscoveryService catalogDiscoveryService,
        CatalogPersistenceService catalogPersistenceService,
        SourceRunTrackingService sourceRunTrackingService)
    {
        _scrapers = scrapers;
        _retailerRegistry = retailerRegistry;
        _catalogDiscoveryService = catalogDiscoveryService;
        _catalogPersistenceService = catalogPersistenceService;
        _sourceRunTrackingService = sourceRunTrackingService;
    }

    public async Task<ScrapeProductResponse> ScrapeAsync(ScrapeProductRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url) ||
            !Uri.TryCreate(request.Url, UriKind.Absolute, out var url))
        {
            throw new ArgumentException("Inserisci un URL assoluto valido.", nameof(request.Url));
        }

        var definition = request.Retailer.HasValue
            ? _retailerRegistry.Get(request.Retailer.Value)
            : _retailerRegistry.TryResolve(url, out var resolvedDefinition)
                ? resolvedDefinition!
                : throw new ArgumentException("Retailer non supportato per l'URL fornito.", nameof(request.Url));

        var scraper = _scrapers.FirstOrDefault(candidate => candidate.Retailer == definition.Retailer);
        if (scraper is null)
        {
            throw new InvalidOperationException($"Nessuno scraper registrato per {definition.DisplayName}.");
        }

        var sourceRun = await _sourceRunTrackingService.StartAsync(
            definition,
            "SingleProductScrape",
            $"Scraping URL {url}",
            cancellationToken);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await scraper.ScrapeAsync(url, request.Mode, cancellationToken);
            stopwatch.Stop();

            result.Product.ScrapedAtUtc = DateTimeOffset.UtcNow;
            result.Product.Retailer = definition.DisplayName;
            result.Product.Method = result.AppliedMode.ToString();

            CatalogPersistenceResult? persistenceResult = null;
            var succeeded = !string.IsNullOrWhiteSpace(result.Product.Title) || result.Product.Price.HasValue;
            if (succeeded)
            {
                persistenceResult = await _catalogPersistenceService.PersistScrapeResultAsync(
                    definition,
                    result.Product,
                    cancellationToken);
            }

            await _sourceRunTrackingService.CompleteAsync(
                sourceRun.Id,
                succeeded,
                itemsFound: succeeded ? 1 : 0,
                itemsSaved: persistenceResult is null ? 0 : 1,
                errorText: succeeded ? null : "Scraping completato senza dati utili.",
                warnings: result.Warnings,
                cancellationToken);

            return new ScrapeProductResponse
            {
                Success = succeeded,
                Message = "Scraping completato.",
                Retailer = definition.Retailer,
                RequestedMode = request.Mode,
                AppliedMode = result.AppliedMode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                AppliedStrategies = result.AppliedStrategies,
                Warnings = result.Warnings,
                Product = result.Product,
                CanonicalProductId = persistenceResult?.CanonicalProductId,
                SourceProductId = persistenceResult?.SourceProductId,
                ProductOfferId = persistenceResult?.ProductOfferId
            };
        }
        catch (Exception exception)
        {
            await _sourceRunTrackingService.CompleteAsync(
                sourceRun.Id,
                succeeded: false,
                itemsFound: 0,
                itemsSaved: 0,
                errorText: exception.Message,
                warnings: null,
                cancellationToken);

            throw;
        }
    }

    public IReadOnlyCollection<RetailerInfo> GetRetailers()
    {
        return BuildRetailers(null);
    }

    public IReadOnlyCollection<RetailerInfo> GetRetailers(RetailerCategory category)
    {
        return BuildRetailers(category);
    }

    private IReadOnlyCollection<RetailerInfo> BuildRetailers(RetailerCategory? category)
    {
        return _retailerRegistry.GetDefinitions()
            .Where(definition => !category.HasValue || definition.Category == category.Value)
            .Select(definition => new RetailerInfo
            {
                Retailer = definition.Retailer,
                DisplayName = definition.DisplayName,
                Category = definition.Category,
                Hosts = definition.Hosts,
                SitemapIndexUrl = definition.SitemapIndexUrl,
                SupportsCatalogDiscovery = definition.SupportsCatalogDiscovery,
                CatalogNotes = definition.CatalogNotes,
                CatalogCoverage = _catalogDiscoveryService.GetCoverageStatus(definition.Retailer),
                SupportedModes = definition.SupportedModes
            })
            .ToArray();
    }
}
