using System.Net;
using OmnilensScraping.Scraping;

namespace OmnilensScraping;

public static class DependencyExtensions
{
    public static IServiceCollection AddOmnilensScraping(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScrapingOptions>(configuration.GetSection("Scraping"));
        services.Configure<AmazonCatalogOptions>(configuration.GetSection("AmazonCatalog"));
        services.Configure<CatalogBootstrapOptions>(configuration.GetSection("CatalogBootstrap"));
        services.Configure<CatalogRefreshOptions>(configuration.GetSection("CatalogRefresh"));

        services.AddHttpClient(PageContentService.ClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(ScrapingOptions.DefaultUserAgent);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.Deflate | DecompressionMethods.GZip
        });

        services.AddSingleton<RetailerRegistry>();
        services.AddSingleton<PageContentService>();
        services.AddSingleton<PlaywrightBrowserService>();
        services.AddSingleton<SitemapCatalogSnapshotService>();
        services.AddSingleton<AmazonCatalogBootstrapService>();
        services.AddSingleton<RetailerCatalogBootstrapService>();
        services.AddSingleton<ICatalogUrlSource, SitemapCatalogUrlSource>();
        services.AddSingleton<ICatalogUrlSource, BootstrapCatalogUrlSource>();
        services.AddSingleton<ICatalogUrlSource, AmazonCatalogUrlSource>();
        services.AddSingleton<CatalogDiscoveryService>();
        services.AddSingleton<ParallelScrapingService>();
        services.AddHostedService<CatalogRefreshHostedService>();

        services.AddScoped<IRetailerScraper, UnieuroRetailerScraper>();
        services.AddScoped<IRetailerScraper, MediaWorldRetailerScraper>();
        services.AddScoped<IRetailerScraper, EuronicsRetailerScraper>();
        services.AddScoped<IRetailerScraper, AmazonItRetailerScraper>();
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.Redcare,
            new[] { "redcare.it", "www.redcare.it" },
            "Redcare"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.DrMax,
            new[] { "drmax.it", "www.drmax.it" },
            "Dr. Max"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.Farmasave,
            new[] { "farmasave.it", "www.farmasave.it" },
            "Farmasave"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.FarmaciaLoreto,
            new[] { "farmacialoreto.it", "www.farmacialoreto.it" },
            "Farmacia Loreto"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.EFarma,
            new[] { "efarma.com", "www.efarma.com" },
            "eFarma"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.Farmacie1000,
            new[] { "1000farmacie.it", "www.1000farmacie.it" },
            "1000 Farmacie"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.TopFarmacia,
            new[] { "topfarmacia.it", "www.topfarmacia.it" },
            "Top Farmacia"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.TuttoFarma,
            new[] { "tuttofarma.it", "www.tuttofarma.it" },
            "TuttoFarma"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.FarmaIt,
            new[] { "farma.it", "www.farma.it", "anticafarmaciaorlandi.it", "www.anticafarmaciaorlandi.it" },
            "Farma.it"));
        services.AddScoped<IRetailerScraper>(serviceProvider => new GenericStructuredRetailerScraper(
            serviceProvider.GetRequiredService<PageContentService>(),
            Models.RetailerType.BenuFarma,
            new[] { "benufarma.it", "www.benufarma.it" },
            "BENU Farma"));
        services.AddScoped<ScrapingCoordinator>();

        return services;
    }
}
