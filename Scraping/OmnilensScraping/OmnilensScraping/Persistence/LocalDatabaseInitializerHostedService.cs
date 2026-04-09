using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence.Entities;
using OmnilensScraping.Scraping;

namespace OmnilensScraping.Persistence;

public sealed class LocalDatabaseInitializerHostedService : IHostedService
{
    private static readonly PharmacyLocationSeed[] PharmacyLocationSeeds =
    {
        new("Via Torino 15", "Milano", "MI", "20123", 45.4637m, 9.1866m),
        new("Via del Corso 120", "Roma", "RM", "00186", 41.9026m, 12.4798m),
        new("Via Toledo 220", "Napoli", "NA", "80132", 40.8380m, 14.2488m),
        new("Via Maqueda 148", "Palermo", "PA", "90133", 38.1157m, 13.3613m),
        new("Via Indipendenza 34", "Bologna", "BO", "40121", 44.4960m, 11.3426m),
        new("Via Etnea 190", "Catania", "CT", "95131", 37.5109m, 15.0871m)
    };

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

        await SeedIdentityAsync(dbContext, cancellationToken);
        await SeedPlansAsync(dbContext, cancellationToken);

        if (options.SeedSourcesFromRegistry)
        {
            await SyncSourcesAsync(dbContext, cancellationToken);
        }

        await EnsureUserDefaultsAsync(dbContext, cancellationToken);
        await SeedPharmacyVerticalAsync(dbContext, cancellationToken);
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

        await RecoverIncorrectLatestBaselineAsync(dbContext, cancellationToken);

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
            source.SupportsApiCollection = false;
            source.SupportsScrapingCollection = true;
            source.SupportsManualCollection = false;
            source.IsEnabled = true;
            source.PriorityScore = GetPriorityScore(definition.Category);
            if (source.HealthScore <= 0m && source.IsEnabled)
            {
                source.HealthScore = 100m;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SQLite locale inizializzato. Retailer sincronizzati: {Count}. Creati: {Created}. Aggiornati: {Updated}.",
            definitions.Count,
            created,
            updated);
    }

    private static async Task SeedIdentityAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var permissions = new[]
        {
            ("catalog.read", "Catalog Read", "Catalog"),
            ("wishlist.manage", "Wishlist Manage", "B2C"),
            ("alerts.manage", "Alerts Manage", "B2C"),
            ("profile.manage", "Profile Manage", "Identity"),
            ("subscriptions.read", "Subscriptions Read", "Billing"),
            ("subscriptions.manage", "Subscriptions Manage", "Billing"),
            ("team.manage", "Team Manage", "B2B"),
            ("analytics.read", "Analytics Read", "B2B"),
            ("exports.read", "Exports Read", "B2B"),
            ("sources.admin", "Sources Admin", "Admin")
        };

        var permissionMap = await dbContext.Permissions
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var (code, displayName, scope) in permissions)
        {
            if (!permissionMap.TryGetValue(code, out var permission))
            {
                permission = new AppPermission { Code = code };
                dbContext.Permissions.Add(permission);
                permissionMap[code] = permission;
            }

            permission.DisplayName = displayName;
            permission.Scope = scope;
        }

        var roles = new[]
        {
            ("B2C", "Consumer", new[] { "catalog.read", "wishlist.manage", "alerts.manage", "profile.manage", "subscriptions.read" }),
            ("B2B", "Business", new[] { "catalog.read", "profile.manage", "subscriptions.read", "analytics.read", "exports.read", "team.manage" }),
            ("PARTNER", "Partner", new[] { "catalog.read", "profile.manage", "subscriptions.read" }),
            ("ADMIN", "Administrator", permissions.Select(item => item.Item1).ToArray())
        };

        var roleMap = await dbContext.Roles
            .Include(item => item.RolePermissions)
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var (code, displayName, rolePermissions) in roles)
        {
            if (!roleMap.TryGetValue(code, out var role))
            {
                role = new AppRole { Code = code };
                dbContext.Roles.Add(role);
                roleMap[code] = role;
            }

            role.DisplayName = displayName;
            role.IsSystem = true;

            var existingCodes = role.RolePermissions
                .Select(item => permissionMap.Values.First(permission => permission.Id == item.PermissionId).Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var permissionCode in rolePermissions.Except(existingCodes, StringComparer.OrdinalIgnoreCase))
            {
                role.RolePermissions.Add(new RolePermission
                {
                    Permission = permissionMap[permissionCode]
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPlansAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var families = new[]
        {
            new { Code = "OMNILENS_CONSUMER", Name = "OmniLens+ Consumer", Audience = "B2C", Description = "Piani consumer per ricerca, wishlist e alert.", SortOrder = 10 },
            new { Code = "OMNILENS_BUSINESS", Name = "OmniLens+ Business", Audience = "B2B", Description = "Piani business per benchmark e analytics.", SortOrder = 20 }
        };

        var familyMap = await dbContext.ProductFamilies
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var familySeed in families)
        {
            if (!familyMap.TryGetValue(familySeed.Code, out var family))
            {
                family = new ProductFamily { Code = familySeed.Code };
                dbContext.ProductFamilies.Add(family);
                familyMap[familySeed.Code] = family;
            }

            family.Name = familySeed.Name;
            family.Audience = familySeed.Audience;
            family.Description = familySeed.Description;
            family.IsActive = true;
            family.SortOrder = familySeed.SortOrder;
        }

        var entitlements = new[]
        {
            new { Code = "SEARCH_DAILY_LIMIT", Name = "Daily searches", Category = "Catalog", ValueType = "Number", Description = "Numero massimo ricerche giornaliere." },
            new { Code = "ALERT_LIMIT", Name = "Alerts", Category = "B2C", ValueType = "Number", Description = "Numero di alert attivi." },
            new { Code = "PRICE_HISTORY_DAYS", Name = "Price history days", Category = "Catalog", ValueType = "Number", Description = "Giorni di storico prezzi accessibili." },
            new { Code = "CSV_EXPORT", Name = "CSV export", Category = "B2B", ValueType = "Boolean", Description = "Accesso export CSV." },
            new { Code = "TEAM_MEMBERS", Name = "Team members", Category = "B2B", ValueType = "Number", Description = "Numero membri aziendali inclusi." }
        };

        var entitlementMap = await dbContext.Entitlements
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var entitlementSeed in entitlements)
        {
            if (!entitlementMap.TryGetValue(entitlementSeed.Code, out var entitlement))
            {
                entitlement = new Entitlement { Code = entitlementSeed.Code };
                dbContext.Entitlements.Add(entitlement);
                entitlementMap[entitlementSeed.Code] = entitlement;
            }

            entitlement.Name = entitlementSeed.Name;
            entitlement.Category = entitlementSeed.Category;
            entitlement.ValueType = entitlementSeed.ValueType;
            entitlement.Description = entitlementSeed.Description;
        }

        var plans = new[]
        {
            new
            {
                Code = "B2C_FREE",
                FamilyCode = "OMNILENS_CONSUMER",
                Name = "Consumer Free",
                Audience = "B2C",
                BillingPeriod = "Monthly",
                Price = 0m,
                Currency = "EUR",
                Description = "Piano base consumer locale.",
                SortOrder = 10,
                Entitlements = new (string Code, decimal? Numeric, bool? Boolean, string? Text)[]
                {
                    ("SEARCH_DAILY_LIMIT", 50m, null, null),
                    ("ALERT_LIMIT", 5m, null, null),
                    ("PRICE_HISTORY_DAYS", 30m, null, null)
                }
            },
            new
            {
                Code = "B2C_PLUS",
                FamilyCode = "OMNILENS_CONSUMER",
                Name = "Consumer Plus",
                Audience = "B2C",
                BillingPeriod = "Monthly",
                Price = 9.99m,
                Currency = "EUR",
                Description = "Piano premium consumer locale.",
                SortOrder = 20,
                Entitlements = new (string Code, decimal? Numeric, bool? Boolean, string? Text)[]
                {
                    ("SEARCH_DAILY_LIMIT", 500m, null, null),
                    ("ALERT_LIMIT", 50m, null, null),
                    ("PRICE_HISTORY_DAYS", 365m, null, null)
                }
            },
            new
            {
                Code = "B2B_STARTER",
                FamilyCode = "OMNILENS_BUSINESS",
                Name = "Business Starter",
                Audience = "B2B",
                BillingPeriod = "Monthly",
                Price = 49.99m,
                Currency = "EUR",
                Description = "Piano business locale con export base.",
                SortOrder = 10,
                Entitlements = new (string Code, decimal? Numeric, bool? Boolean, string? Text)[]
                {
                    ("SEARCH_DAILY_LIMIT", 5000m, null, null),
                    ("PRICE_HISTORY_DAYS", 365m, null, null),
                    ("CSV_EXPORT", null, true, null),
                    ("TEAM_MEMBERS", 5m, null, null)
                }
            }
        };

        var planMap = await dbContext.Plans
            .Include(item => item.PlanEntitlements)
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var planSeed in plans)
        {
            if (!planMap.TryGetValue(planSeed.Code, out var plan))
            {
                plan = new Plan { Code = planSeed.Code };
                dbContext.Plans.Add(plan);
                planMap[planSeed.Code] = plan;
            }

            plan.ProductFamily = familyMap[planSeed.FamilyCode];
            plan.Name = planSeed.Name;
            plan.Audience = planSeed.Audience;
            plan.BillingPeriod = planSeed.BillingPeriod;
            plan.Price = planSeed.Price;
            plan.Currency = planSeed.Currency;
            plan.Description = planSeed.Description;
            plan.IsActive = true;
            plan.SortOrder = planSeed.SortOrder;

            var existingEntitlements = plan.PlanEntitlements
                .ToDictionary(item => item.EntitlementId, item => item);

            foreach (var entitlementSeed in planSeed.Entitlements)
            {
                var entitlement = entitlementMap[entitlementSeed.Code];
                if (!existingEntitlements.TryGetValue(entitlement.Id, out var planEntitlement))
                {
                    planEntitlement = new PlanEntitlement
                    {
                        Entitlement = entitlement
                    };
                    plan.PlanEntitlements.Add(planEntitlement);
                }

                planEntitlement.NumericValue = entitlementSeed.Numeric;
                planEntitlement.BooleanValue = entitlementSeed.Boolean;
                planEntitlement.StringValue = entitlementSeed.Text;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureUserDefaultsAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var roleMap = await dbContext.Roles
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var planMap = await dbContext.Plans
            .Include(item => item.ProductFamily)
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var users = await dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var roleCode = NormalizeUserType(user.UserType);
            if (user.UserType != roleCode)
            {
                var trackedUser = await dbContext.Users.SingleAsync(item => item.Id == user.Id, cancellationToken);
                trackedUser.UserType = roleCode;
            }

            if (roleMap.TryGetValue(roleCode, out var role) &&
                !await dbContext.UserRoles.AnyAsync(item => item.UserId == user.Id && item.RoleId == role.Id, cancellationToken))
            {
                dbContext.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }

            if (!await dbContext.UserProfileSettings.AnyAsync(item => item.UserId == user.Id, cancellationToken))
            {
                dbContext.UserProfileSettings.Add(new UserProfileSettings
                {
                    UserId = user.Id,
                    LanguageCode = "it",
                    CountryCode = user.CountryCode ?? "IT",
                    PrivacyConsentAccepted = true,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }

            var hasActiveSubscription = await dbContext.UserSubscriptions
                .AnyAsync(item => item.UserId == user.Id && item.Status == "Active", cancellationToken);

            if (!hasActiveSubscription)
            {
                var defaultPlanCode = roleCode switch
                {
                    "B2B" => "B2B_STARTER",
                    "B2C" => "B2C_FREE",
                    _ => null
                };

                if (defaultPlanCode is not null && planMap.TryGetValue(defaultPlanCode, out var plan))
                {
                    dbContext.UserSubscriptions.Add(new UserSubscription
                    {
                        UserId = user.Id,
                        PlanId = plan.Id,
                        Status = "Active",
                        AutoRenew = true,
                        StartedAtUtc = DateTimeOffset.UtcNow,
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPharmacyVerticalAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var locationsCreated = await SeedPharmacyLocationsAsync(dbContext, cancellationToken);
        var factsCreated = await SeedPharmacyFactsAsync(dbContext, cancellationToken);

        if (locationsCreated == 0 && factsCreated == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bootstrap verticale farmacia completato. Sedi create: {Locations}. Schede prodotto create/aggiornate: {Facts}.",
            locationsCreated,
            factsCreated);
    }

    private static async Task<int> SeedPharmacyLocationsAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var pharmacySources = await dbContext.Sources
            .Where(item => item.Category == RetailerCategory.Pharmacy.ToString())
            .OrderBy(item => item.RetailerCode)
            .ToListAsync(cancellationToken);

        var existingSourceIds = await dbContext.PharmacyLocations
            .Select(item => item.SourceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var created = 0;

        for (var index = 0; index < pharmacySources.Count; index++)
        {
            var source = pharmacySources[index];
            if (existingSourceIds.Contains(source.Id))
            {
                continue;
            }

            var seed = PharmacyLocationSeeds[index % PharmacyLocationSeeds.Length];
            dbContext.PharmacyLocations.Add(new PharmacyLocation
            {
                SourceId = source.Id,
                Name = $"{source.DisplayName} {seed.City}",
                Address = seed.Address,
                City = seed.City,
                Province = seed.Province,
                PostalCode = seed.PostalCode,
                Latitude = seed.Latitude,
                Longitude = seed.Longitude,
                OpeningHoursJson = JsonSerializer.Serialize(new[]
                {
                    new { days = "Mon-Sat", hours = "08:30-20:00" },
                    new { days = "Sun", hours = "09:00-13:00" }
                })
            });

            created++;
        }

        return created;
    }

    private static async Task<int> SeedPharmacyFactsAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        var products = await dbContext.CanonicalProducts
            .Include(item => item.Attributes)
            .Include(item => item.PharmacyProductFact)
            .Where(item => item.Vertical == RetailerCategory.Pharmacy.ToString())
            .ToListAsync(cancellationToken);

        var touched = 0;

        foreach (var product in products)
        {
            var fact = product.PharmacyProductFact;
            if (fact is null)
            {
                fact = new PharmacyProductFact
                {
                    CanonicalProductId = product.Id
                };

                dbContext.PharmacyProductFacts.Add(fact);
            }

            var attributes = product.Attributes
                .ToDictionary(item => item.AttributeName, item => item.AttributeValue, StringComparer.OrdinalIgnoreCase);
            var categoryClass = InferPharmacyCategoryClass(product);

            if (string.IsNullOrWhiteSpace(fact.CategoryClass) ||
                fact.CategoryClass.Equals("OTC", StringComparison.OrdinalIgnoreCase))
            {
                fact.CategoryClass = categoryClass;
            }
            fact.Manufacturer = string.IsNullOrWhiteSpace(fact.Manufacturer) ? product.Brand : fact.Manufacturer;
            fact.ActiveIngredient ??= ExtractActiveIngredient(product, attributes);
            fact.DosageForm ??= ExtractDosageForm(product);
            fact.StrengthText ??= ExtractStrength(product);
            fact.PackageSize ??= ExtractPackageSize(product);
            fact.RequiresPrescription = InferRequiresPrescription(product);
            fact.IsOtc = fact.CategoryClass.Equals("OTC", StringComparison.OrdinalIgnoreCase);
            fact.IsSop = fact.CategoryClass.Equals("SOP", StringComparison.OrdinalIgnoreCase);

            touched++;
        }

        return touched;
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

    private static string NormalizeUserType(string? userType)
    {
        return userType?.Trim().ToUpperInvariant() switch
        {
            "B2C" => "B2C",
            "B2B" => "B2B",
            "ADMIN" => "ADMIN",
            "PARTNER" => "PARTNER",
            _ => "B2C"
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
        var baselineMigration = dbContext.Database.GetMigrations().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(baselineMigration))
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
                ["@migrationId"] = baselineMigration
            },
            cancellationToken);

        if (existingBaseline == 0)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({baselineMigration}, {GetEntityFrameworkProductVersion()});
                """,
                cancellationToken);
        }

        _logger.LogWarning(
            "Database SQLite esistente rilevato senza storico migration. Applicata baseline locale alla migration {MigrationId}.",
            baselineMigration);
    }

    private async Task RecoverIncorrectLatestBaselineAsync(OmnilensDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(dbContext, "__EFMigrationsHistory", cancellationToken))
        {
            return;
        }

        var migrations = dbContext.Database.GetMigrations().ToArray();
        if (migrations.Length <= 1)
        {
            return;
        }

        var appliedMigrations = dbContext.Database.GetAppliedMigrations().ToArray();
        if (appliedMigrations.Length == 0)
        {
            return;
        }

        var latestApplied = appliedMigrations[^1];
        var latestAvailable = migrations[^1];
        if (!string.Equals(latestApplied, latestAvailable, StringComparison.Ordinal))
        {
            return;
        }

        var latestSchemaPresent = await TableExistsAsync(dbContext, "permissions", cancellationToken) &&
                                  await TableExistsAsync(dbContext, "plans", cancellationToken) &&
                                  await TableExistsAsync(dbContext, "user_refresh_tokens", cancellationToken);

        if (latestSchemaPresent)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = {latestApplied};""",
            cancellationToken);

        _logger.LogWarning(
            "Rilevata baseline migration incoerente sul DB locale. Rimossa la voce {MigrationId} per rieseguire le migration mancanti.",
            latestApplied);
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

    private static string InferPharmacyCategoryClass(CanonicalProduct product)
    {
        var text = BuildPharmacySearchText(product);

        if (ContainsAny(text, "integratore", "vitamina", "omega", "magnesio", "probiot", "supplement"))
        {
            return "Integratore";
        }

        if (ContainsAny(text, "termometro", "misuratore", "dispositivo", "cerotti", "test ", "tampone", "mascherina"))
        {
            return "Dispositivo";
        }

        if (ContainsAny(text, "eau de", "profumo", "parfum", "cosmetic", "crema", "shampoo", "balsamo", "solare"))
        {
            return "Cosmetico";
        }

        if (ContainsAny(text, "collirio", "spray", "sciroppo", "gocce", "compresse", "capsule", "bustine"))
        {
            return "SOP";
        }

        return "OTC";
    }

    private static string? ExtractActiveIngredient(
        CanonicalProduct product,
        IReadOnlyDictionary<string, string> attributes)
    {
        foreach (var key in new[] { "Principio attivo", "Active ingredient", "Ingredienti", "Composizione" })
        {
            if (attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        var description = product.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            var match = Regex.Match(
                description,
                @"(?:principio attivo|active ingredient)\s*[:\-]?\s*([A-Za-z0-9 ,\-/]{3,120})",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim(' ', ',', '.', ';', ':');
            }
        }

        return null;
    }

    private static string? ExtractDosageForm(CanonicalProduct product)
    {
        var text = BuildPharmacySearchText(product);
        var forms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["eau de toilette"] = "Eau de Toilette",
            ["eau de parfum"] = "Eau de Parfum",
            ["compresse"] = "Compresse",
            ["capsule"] = "Capsule",
            ["spray"] = "Spray",
            ["crema"] = "Crema",
            ["gel"] = "Gel",
            ["sciroppo"] = "Sciroppo",
            ["gocce"] = "Gocce",
            ["bustine"] = "Bustine"
        };

        return forms.FirstOrDefault(item => text.Contains(item.Key, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static string? ExtractStrength(CanonicalProduct product)
    {
        var text = BuildPharmacySearchText(product);
        var match = Regex.Match(text, @"\b\d+\s?(?:mg|mcg|g)(?:\/\d+\s?(?:ml|g))?\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string? ExtractPackageSize(CanonicalProduct product)
    {
        var text = BuildPharmacySearchText(product);
        var match = Regex.Match(text, @"\b\d+\s?(?:ml|g|kg|capsule|compresse|bustine|fiale)\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : null;
    }

    private static bool InferRequiresPrescription(CanonicalProduct product)
    {
        var text = BuildPharmacySearchText(product);
        return ContainsAny(text, "ricetta", "prescrizione", "rx", "antibiot", "stupefacente");
    }

    private static string BuildPharmacySearchText(CanonicalProduct product)
    {
        return string.Join(' ',
            new[]
            {
                product.Title,
                product.Brand,
                product.Description,
                string.Join(' ', product.Attributes.Select(item => item.AttributeValue))
            }.Where(item => !string.IsNullOrWhiteSpace(item)))
            .Trim();
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record PharmacyLocationSeed(
        string Address,
        string City,
        string Province,
        string PostalCode,
        decimal Latitude,
        decimal Longitude);
}
