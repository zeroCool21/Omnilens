using Microsoft.Extensions.Options;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class CatalogRefreshHostedService : BackgroundService
{
    private readonly RetailerRegistry _retailerRegistry;
    private readonly SitemapCatalogSnapshotService _sitemapSnapshotService;
    private readonly AmazonCatalogBootstrapService _amazonBootstrapService;
    private readonly RetailerCatalogBootstrapService _retailerBootstrapService;
    private readonly AmazonCatalogOptions _amazonOptions;
    private readonly CatalogRefreshOptions _refreshOptions;
    private readonly ILogger<CatalogRefreshHostedService> _logger;

    public CatalogRefreshHostedService(
        RetailerRegistry retailerRegistry,
        SitemapCatalogSnapshotService sitemapSnapshotService,
        AmazonCatalogBootstrapService amazonBootstrapService,
        RetailerCatalogBootstrapService retailerBootstrapService,
        IOptions<AmazonCatalogOptions> amazonOptions,
        IOptions<CatalogRefreshOptions> refreshOptions,
        ILogger<CatalogRefreshHostedService> logger)
    {
        _retailerRegistry = retailerRegistry;
        _sitemapSnapshotService = sitemapSnapshotService;
        _amazonBootstrapService = amazonBootstrapService;
        _retailerBootstrapService = retailerBootstrapService;
        _amazonOptions = amazonOptions.Value;
        _refreshOptions = refreshOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_refreshOptions.Enabled)
        {
            _logger.LogInformation("Catalog refresh periodico disabilitato.");
            return;
        }

        if (_refreshOptions.RunOnStartup)
        {
            var initialDelay = Math.Max(0, _refreshOptions.InitialDelaySeconds);
            if (initialDelay > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(initialDelay), stoppingToken);
            }

            await RefreshAllCatalogsAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _refreshOptions.CheckIntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAllCatalogsAsync(stoppingToken);
        }
    }

    private async Task RefreshAllCatalogsAsync(CancellationToken cancellationToken)
    {
        foreach (var definition in _retailerRegistry.GetDefinitions())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                switch (definition.Retailer)
                {
                    case RetailerType.AmazonIt:
                        await RefreshAmazonCatalogAsync(cancellationToken);
                        break;
                    default:
                        if (_retailerBootstrapService.CanBootstrap(definition))
                        {
                            await RefreshBootstrapCatalogAsync(definition, cancellationToken);
                        }
                        else if (!string.IsNullOrWhiteSpace(definition.SitemapIndexUrl) &&
                                 definition.ProductSitemapMarkers.Count > 0)
                        {
                            var sourceCount = await _sitemapSnapshotService.GetOrRefreshProductSourceCountAsync(definition, cancellationToken);
                            _logger.LogInformation(
                                "Snapshot catalogo {Retailer} aggiornata o confermata. Product sources: {SourceCount}",
                                definition.DisplayName,
                                sourceCount);
                        }

                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Refresh catalogo fallito per {Retailer}", definition.DisplayName);
            }
        }
    }

    private async Task RefreshAmazonCatalogAsync(CancellationToken cancellationToken)
    {
        if (HasAuthoritativeAmazonCatalog())
        {
            _logger.LogInformation("Snapshot autorevole Amazon configurata. Il refresh pubblico viene saltato.");
            return;
        }

        var response = await _amazonBootstrapService.EnsurePublicSnapshotAsync(force: false, cancellationToken: cancellationToken);
        if (response is null)
        {
            _logger.LogInformation("Snapshot pubblica Amazon IT ancora fresca. Nessun bootstrap necessario.");
            return;
        }

        _logger.LogInformation(
            "Bootstrap Amazon IT completato. Prodotti persistiti: {PersistedProducts}, pagine crawlate: {CrawledPages}, sitemap generate: {GeneratedSitemaps}",
            response.PersistedProducts,
            response.CrawledPages,
            response.GeneratedSitemaps);
    }

    private async Task RefreshBootstrapCatalogAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var response = await _retailerBootstrapService.EnsurePublicSnapshotAsync(definition, force: false, cancellationToken: cancellationToken);
        if (response is null)
        {
            _logger.LogInformation("Snapshot pubblica {Retailer} ancora fresca. Nessun bootstrap necessario.", definition.DisplayName);
            return;
        }

        _logger.LogInformation(
            "Bootstrap {Retailer} completato. Prodotti persistiti: {PersistedProducts}, pagine crawlate: {CrawledPages}, sitemap generate: {GeneratedSitemaps}",
            definition.DisplayName,
            response.PersistedProducts,
            response.CrawledPages,
            response.GeneratedSitemaps);
    }

    private bool HasAuthoritativeAmazonCatalog()
    {
        foreach (var path in _amazonOptions.AuthoritativeFilePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (File.Exists(Path.GetFullPath(path)))
            {
                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(_amazonOptions.AuthoritativeCatalogDirectory))
        {
            return false;
        }

        var directory = Path.GetFullPath(_amazonOptions.AuthoritativeCatalogDirectory);
        if (!Directory.Exists(directory))
        {
            return false;
        }

        foreach (var pattern in _amazonOptions.AuthoritativeFilePatterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern)))
        {
            if (Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories).Any())
            {
                return true;
            }
        }

        return false;
    }
}
