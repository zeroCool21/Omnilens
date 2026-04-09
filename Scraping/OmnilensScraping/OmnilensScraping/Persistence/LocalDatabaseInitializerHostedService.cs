using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence.Entities;
using OmnilensScraping.Scraping;

namespace OmnilensScraping.Persistence;

public sealed class LocalDatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DatabaseOptions> _databaseOptions;
    private readonly RetailerRegistry _retailerRegistry;
    private readonly ILogger<LocalDatabaseInitializerHostedService> _logger;

    public LocalDatabaseInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DatabaseOptions> databaseOptions,
        RetailerRegistry retailerRegistry,
        ILogger<LocalDatabaseInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _databaseOptions = databaseOptions;
        _retailerRegistry = retailerRegistry;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OmnilensDbContext>();
        var options = _databaseOptions.Value;

        if (options.AutoCreateSchema)
        {
            await EnsureSchemaAsync(dbContext, cancellationToken);
        }

        if (options.SeedSourcesFromRegistry)
        {
            await SyncSourcesAsync(dbContext, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task EnsureSchemaAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var hasMigrationHistory = await TableExistsAsync(dbContext, "__EFMigrationsHistory", cancellationToken);
        if (!hasMigrationHistory && await HasApplicationTablesAsync(dbContext, cancellationToken))
        {
            await BaselineExistingSchemaAsync(dbContext, cancellationToken);
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private async Task SyncSourcesAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var definitions = _retailerRegistry.GetDefinitions();
        var existingSources = await dbContext.Sources
            .ToDictionaryAsync(item => item.RetailerCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var created = 0;
        var updated = 0;

        foreach (var definition in definitions)
        {
            var retailerCode = definition.Retailer.ToString();
            if (!existingSources.TryGetValue(retailerCode, out var source))
            {
                source = new Source
                {
                    RetailerCode = retailerCode,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

                dbContext.Sources.Add(source);
                created++;
            }
            else
            {
                updated++;
            }

            source.DisplayName = definition.DisplayName;
            source.Category = definition.Category.ToString();
            source.CountryCode = "IT";
            source.BaseUrl = ResolveBaseUrl(definition);
            source.SupportsCatalogBootstrap = definition.SupportsCatalogDiscovery;
            source.SupportsLiveScrape = true;
            source.IsEnabled = true;
            source.PriorityScore = GetPriorityScore(definition.Category);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SQLite locale inizializzato. Retailer sincronizzati: {Count}. Creati: {Created}. Aggiornati: {Updated}.",
            definitions.Count,
            created,
            updated);
    }

    private static string ResolveBaseUrl(RetailerDefinition definition)
    {
        if (Uri.TryCreate(definition.SitemapIndexUrl, UriKind.Absolute, out var sitemapUri))
        {
            return $"{sitemapUri.Scheme}://{sitemapUri.Host}";
        }

        var host = definition.Hosts.FirstOrDefault();
        return string.IsNullOrWhiteSpace(host)
            ? string.Empty
            : $"https://{host}";
    }

    private static int GetPriorityScore(RetailerCategory category)
    {
        return category switch
        {
            RetailerCategory.Pharmacy => 100,
            RetailerCategory.Electronics => 80,
            RetailerCategory.Marketplace => 70,
            _ => 50
        };
    }

    private static async Task<bool> TableExistsAsync(
        OmnilensDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        var count = await ExecuteScalarAsync(
            dbContext,
            "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @name;",
            new Dictionary<string, object?>
            {
                ["@name"] = tableName
            },
            cancellationToken);

        return count > 0;
    }

    private static async Task<bool> HasApplicationTablesAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var count = await ExecuteScalarAsync(
            dbContext,
            """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('users', 'sources', 'canonical_products', 'source_products', 'product_offers');
            """,
            parameters: null,
            cancellationToken);

        return count > 0;
    }

    private async Task BaselineExistingSchemaAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var latestMigration = dbContext.Database.GetMigrations().LastOrDefault();
        if (string.IsNullOrWhiteSpace(latestMigration))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """,
            cancellationToken);

        var existingBaseline = await ExecuteScalarAsync(
            dbContext,
            "SELECT COUNT(1) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @migrationId;",
            new Dictionary<string, object?>
            {
                ["@migrationId"] = latestMigration
            },
            cancellationToken);

        if (existingBaseline == 0)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({latestMigration}, {GetEntityFrameworkProductVersion()});
                """,
                cancellationToken);
        }

        _logger.LogWarning(
            "Database SQLite esistente rilevato senza storico migration. Applicata baseline locale alla migration {MigrationId}.",
            latestMigration);
    }

    private static async Task<int> ExecuteScalarAsync(
        OmnilensDbContext dbContext,
        string commandText,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;

            if (parameters is not null)
            {
                foreach (var (name, value) in parameters)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = name;
                    parameter.Value = value ?? DBNull.Value;
                    command.Parameters.Add(parameter);
                }
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                null => 0,
                int intValue => intValue,
                long longValue => checked((int)longValue),
                _ => Convert.ToInt32(result)
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string GetEntityFrameworkProductVersion()
    {
        return typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+', 2)[0]
            ?? "8.0.0";
    }
}
