using System.Net;
using OmnilensScraping.Scraping;

namespace OmnilensScraping;

public static class DependencyExtensions
{
    public static IServiceCollection AddOmnilensScraping(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScrapingOptions>(configuration.GetSection("Scraping"));
        services.Configure<AmazonCatalogOptions>(configuration.GetSection("AmazonCatalog"));
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
        services.AddSingleton<ICatalogUrlSource, SitemapCatalogUrlSource>();
        services.AddSingleton<ICatalogUrlSource, AmazonCatalogUrlSource>();
        services.AddSingleton<CatalogDiscoveryService>();
        services.AddSingleton<ParallelScrapingService>();
        services.AddHostedService<CatalogRefreshHostedService>();

        services.AddScoped<IRetailerScraper, UnieuroRetailerScraper>();
        services.AddScoped<IRetailerScraper, MediaWorldRetailerScraper>();
        services.AddScoped<IRetailerScraper, EuronicsRetailerScraper>();
        services.AddScoped<IRetailerScraper, AmazonItRetailerScraper>();
        services.AddScoped<ScrapingCoordinator>();

        return services;
    }
}
