using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OmnilensScraping.Auth;
using OmnilensScraping.Persistence;
using OmnilensScraping.Scraping;
using OmnilensScraping.Tracking;

namespace OmnilensScraping;

public static class DependencyExtensions
{
    public static IServiceCollection AddOmnilensScraping(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<ScrapingOptions>(configuration.GetSection("Scraping"));
        services.Configure<AmazonCatalogOptions>(configuration.GetSection("AmazonCatalog"));
        services.Configure<CatalogBootstrapOptions>(configuration.GetSection("CatalogBootstrap"));
        services.Configure<CatalogRefreshOptions>(configuration.GetSection("CatalogRefresh"));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<AlertingOptions>(configuration.GetSection("Alerting"));
        services.Configure<ReferralOptions>(configuration.GetSection("Referral"));

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var sqliteConnectionString = BuildSqliteConnectionString(databaseOptions, environment);

        services.AddDbContext<OmnilensDbContext>(options =>
            options.UseSqlite(sqliteConnectionString));

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = authOptions.Issuer,
                    ValidAudience = authOptions.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("ADMIN"));
        });

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
        services.AddSingleton<LocalPasswordHasher>();
        services.AddSingleton<LocalOpaqueTokenService>();
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<IEmailSender, LocalLogEmailSender>();
        services.AddSingleton<ReferralLinkTokenService>();
        services.AddHostedService<LocalDatabaseInitializerHostedService>();
        services.AddHostedService<CatalogRefreshHostedService>();
        services.AddHostedService<AlertEvaluationHostedService>();
        services.AddScoped<CatalogPersistenceService>();
        services.AddScoped<SourceRunTrackingService>();
        services.AddScoped<AlertEvaluationService>();

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

    private static string BuildSqliteConnectionString(DatabaseOptions options, IHostEnvironment environment)
    {
        var filePath = string.IsNullOrWhiteSpace(options.FilePath)
            ? DatabaseOptions.DefaultFilePath
            : options.FilePath;

        var absolutePath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(environment.ContentRootPath, filePath);

        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return $"Data Source={absolutePath}";
    }
}
