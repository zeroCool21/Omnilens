using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Persistence;

public sealed class OmnilensDbContext : DbContext
{
    public OmnilensDbContext(DbContextOptions<OmnilensDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();
    public DbSet<CompanyInvite> CompanyInvites => Set<CompanyInvite>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppPermission> Permissions => Set<AppPermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserProfileSettings> UserProfileSettings => Set<UserProfileSettings>();
    public DbSet<ProductFamily> ProductFamilies => Set<ProductFamily>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Entitlement> Entitlements => Set<Entitlement>();
    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<CanonicalProduct> CanonicalProducts => Set<CanonicalProduct>();
    public DbSet<CanonicalProductAttribute> CanonicalProductAttributes => Set<CanonicalProductAttribute>();
    public DbSet<SourceProduct> SourceProducts => Set<SourceProduct>();
    public DbSet<ProductOffer> ProductOffers => Set<ProductOffer>();
    public DbSet<PriceHistoryEntry> PriceHistory => Set<PriceHistoryEntry>();
    public DbSet<WishlistEntry> WishlistEntries => Set<WishlistEntry>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertDelivery> AlertDeliveries => Set<AlertDelivery>();
    public DbSet<ClickEvent> ClickEvents => Set<ClickEvent>();
    public DbSet<ConversionEvent> ConversionEvents => Set<ConversionEvent>();
    public DbSet<SourceRun> SourceRuns => Set<SourceRun>();
    public DbSet<SourceRunLog> SourceRunLogs => Set<SourceRunLog>();
    public DbSet<PharmacyProductFact> PharmacyProductFacts => Set<PharmacyProductFact>();
    public DbSet<PharmacyLocation> PharmacyLocations => Set<PharmacyLocation>();
    public DbSet<PharmacyReservation> PharmacyReservations => Set<PharmacyReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureIdentity(modelBuilder);
        ConfigureSubscriptions(modelBuilder);
        ConfigureCatalog(modelBuilder);
        ConfigureTracking(modelBuilder);
        ConfigurePharmacy(modelBuilder);
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.UserType).HasMaxLength(32).IsRequired();
            entity.Property(item => item.CountryCode).HasMaxLength(2);
            entity.HasIndex(item => item.Email).IsUnique();
        });

        modelBuilder.Entity<AppRole>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<AppPermission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(128).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Scope).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.RoleId }).IsUnique();
            entity.HasOne(item => item.User)
                .WithMany(item => item.UserRoles)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Role)
                .WithMany(item => item.UserRoles)
                .HasForeignKey(item => item.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.RoleId, item.PermissionId }).IsUnique();
            entity.HasOne(item => item.Role)
                .WithMany(item => item.RolePermissions)
                .HasForeignKey(item => item.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Permission)
                .WithMany(item => item.RolePermissions)
                .HasForeignKey(item => item.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRefreshToken>(entity =>
        {
            entity.ToTable("user_refresh_tokens");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(item => item.UserAgent).HasMaxLength(512);
            entity.Property(item => item.IpAddress).HasMaxLength(128);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.UserId, item.ExpiresAtUtc });
            entity.HasOne(item => item.User)
                .WithMany(item => item.RefreshTokens)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.UserId, item.ExpiresAtUtc });
            entity.HasOne(item => item.User)
                .WithMany(item => item.PasswordResetTokens)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserProfileSettings>(entity =>
        {
            entity.ToTable("user_profile_settings");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.LanguageCode).HasMaxLength(8).IsRequired();
            entity.Property(item => item.CountryCode).HasMaxLength(2).IsRequired();
            entity.Property(item => item.SectorCode).HasMaxLength(64);
            entity.HasOne(item => item.User)
                .WithOne(item => item.ProfileSettings)
                .HasForeignKey<UserProfileSettings>(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.VatCode).HasMaxLength(64);
        });

        modelBuilder.Entity<CompanyMember>(entity =>
        {
            entity.ToTable("company_members");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Role).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.CompanyId, item.UserId }).IsUnique();
            entity.HasOne(item => item.Company)
                .WithMany(item => item.Members)
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.User)
                .WithMany(item => item.CompanyMemberships)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompanyInvite>(entity =>
        {
            entity.ToTable("company_invites");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.Role).HasMaxLength(64).IsRequired();
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.CompanyId, item.Email, item.Status });
            entity.HasOne(item => item.Company)
                .WithMany(item => item.Invites)
                .HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.AcceptedByUser)
                .WithMany()
                .HasForeignKey(item => item.AcceptedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureSubscriptions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductFamily>(entity =>
        {
            entity.ToTable("product_families");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Audience).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(512);
            entity.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("plans");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Audience).HasMaxLength(32).IsRequired();
            entity.Property(item => item.BillingPeriod).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.Price).HasPrecision(12, 2);
            entity.Property(item => item.Description).HasMaxLength(512);
            entity.HasIndex(item => item.Code).IsUnique();
            entity.HasOne(item => item.ProductFamily)
                .WithMany(item => item.Plans)
                .HasForeignKey(item => item.ProductFamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Entitlement>(entity =>
        {
            entity.ToTable("entitlements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ValueType).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(512);
            entity.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<PlanEntitlement>(entity =>
        {
            entity.ToTable("plan_entitlements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.StringValue).HasMaxLength(256);
            entity.Property(item => item.NumericValue).HasPrecision(12, 2);
            entity.HasIndex(item => new { item.PlanId, item.EntitlementId }).IsUnique();
            entity.HasOne(item => item.Plan)
                .WithMany(item => item.PlanEntitlements)
                .HasForeignKey(item => item.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Entitlement)
                .WithMany(item => item.PlanEntitlements)
                .HasForeignKey(item => item.EntitlementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.ToTable("user_subscriptions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.Status });
            entity.HasOne(item => item.User)
                .WithMany(item => item.Subscriptions)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Plan)
                .WithMany(item => item.UserSubscriptions)
                .HasForeignKey(item => item.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Source>(entity =>
        {
            entity.ToTable("sources");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RetailerCode).HasMaxLength(64).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(64).IsRequired();
            entity.Property(item => item.CountryCode).HasMaxLength(2).IsRequired();
            entity.Property(item => item.BaseUrl).HasMaxLength(2048).IsRequired();
            entity.Property(item => item.HealthScore).HasPrecision(5, 2);
            entity.HasIndex(item => item.RetailerCode).IsUnique();
        });

        modelBuilder.Entity<CanonicalProduct>(entity =>
        {
            entity.ToTable("canonical_products");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Slug).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Brand).HasMaxLength(200);
            entity.Property(item => item.CategoryName).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Gtin).HasMaxLength(64);
            entity.Property(item => item.CanonicalSku).HasMaxLength(128);
            entity.Property(item => item.ImageUrl).HasMaxLength(2048);
            entity.Property(item => item.Vertical).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.Slug).IsUnique();
            entity.HasIndex(item => item.Gtin);
        });

        modelBuilder.Entity<CanonicalProductAttribute>(entity =>
        {
            entity.ToTable("canonical_product_attributes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AttributeName).HasMaxLength(128).IsRequired();
            entity.Property(item => item.AttributeValue).IsRequired();
            entity.HasIndex(item => new { item.CanonicalProductId, item.AttributeName });
            entity.HasOne(item => item.CanonicalProduct)
                .WithMany(item => item.Attributes)
                .HasForeignKey(item => item.CanonicalProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceProduct>(entity =>
        {
            entity.ToTable("source_products");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceUrl).HasMaxLength(2048).IsRequired();
            entity.Property(item => item.SourceProductKey).HasMaxLength(128);
            entity.Property(item => item.Title).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Brand).HasMaxLength(200);
            entity.Property(item => item.Sku).HasMaxLength(128);
            entity.Property(item => item.Gtin).HasMaxLength(64);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.Property(item => item.AvailabilityText).HasMaxLength(256);
            entity.Property(item => item.ImageUrl).HasMaxLength(2048);
            entity.HasIndex(item => new { item.SourceId, item.SourceUrl }).IsUnique();
            entity.HasIndex(item => item.CanonicalProductId);
            entity.HasOne(item => item.Source)
                .WithMany(item => item.SourceProducts)
                .HasForeignKey(item => item.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.CanonicalProduct)
                .WithMany(item => item.SourceProducts)
                .HasForeignKey(item => item.CanonicalProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductOffer>(entity =>
        {
            entity.ToTable("product_offers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Price).HasPrecision(12, 2);
            entity.Property(item => item.PriceText).HasMaxLength(128);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.Property(item => item.AvailabilityText).HasMaxLength(256);
            entity.Property(item => item.StockStatus).HasMaxLength(64);
            entity.Property(item => item.ShippingText).HasMaxLength(256);
            entity.Property(item => item.OfferUrl).HasMaxLength(2048).IsRequired();
            entity.HasIndex(item => new { item.SourceProductId, item.IsLatest });
            entity.HasOne(item => item.SourceProduct)
                .WithMany(item => item.ProductOffers)
                .HasForeignKey(item => item.SourceProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PriceHistoryEntry>(entity =>
        {
            entity.ToTable("price_history");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Price).HasPrecision(12, 2);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.Property(item => item.AvailabilityText).HasMaxLength(256);
            entity.HasIndex(item => new { item.SourceProductId, item.RecordedAtUtc });
            entity.HasOne(item => item.SourceProduct)
                .WithMany(item => item.PriceHistoryEntries)
                .HasForeignKey(item => item.SourceProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureTracking(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WishlistEntry>(entity =>
        {
            entity.ToTable("wishlists");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.CanonicalProductId }).IsUnique();
            entity.HasOne(item => item.User)
                .WithMany(item => item.WishlistEntries)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.CanonicalProduct)
                .WithMany(item => item.WishlistEntries)
                .HasForeignKey(item => item.CanonicalProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.ToTable("alert_rules");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TargetPrice).HasPrecision(12, 2);
            entity.HasIndex(item => new { item.UserId, item.CanonicalProductId, item.IsActive });
            entity.HasOne(item => item.User)
                .WithMany(item => item.AlertRules)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.CanonicalProduct)
                .WithMany(item => item.AlertRules)
                .HasForeignKey(item => item.CanonicalProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlertDelivery>(entity =>
        {
            entity.ToTable("alert_deliveries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TriggerReason).HasMaxLength(128).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.HasOne(item => item.AlertRule)
                .WithMany(item => item.AlertDeliveries)
                .HasForeignKey(item => item.AlertRuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClickEvent>(entity =>
        {
            entity.ToTable("click_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OfferUrl).HasMaxLength(2048).IsRequired();
            entity.Property(item => item.UtmSource).HasMaxLength(128);
            entity.Property(item => item.UtmCampaign).HasMaxLength(128);
            entity.HasIndex(item => new { item.SourceId, item.ClickedAtUtc });
            entity.HasOne(item => item.User)
                .WithMany(item => item.ClickEvents)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.CanonicalProduct)
                .WithMany(item => item.ClickEvents)
                .HasForeignKey(item => item.CanonicalProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Source)
                .WithMany(item => item.ClickEvents)
                .HasForeignKey(item => item.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversionEvent>(entity =>
        {
            entity.ToTable("conversion_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ExternalOrderRef).HasMaxLength(128);
            entity.Property(item => item.CommissionAmount).HasPrecision(12, 2);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.HasOne(item => item.ClickEvent)
                .WithMany(item => item.ConversionEvents)
                .HasForeignKey(item => item.ClickEventId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.Source)
                .WithMany(item => item.ConversionEvents)
                .HasForeignKey(item => item.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceRun>(entity =>
        {
            entity.ToTable("source_runs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RunType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.SourceId, item.StartedAtUtc });
            entity.HasOne(item => item.Source)
                .WithMany(item => item.SourceRuns)
                .HasForeignKey(item => item.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceRunLog>(entity =>
        {
            entity.ToTable("source_run_logs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Level).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Message).IsRequired();
            entity.HasIndex(item => new { item.SourceRunId, item.CreatedAtUtc });
            entity.HasOne(item => item.SourceRun)
                .WithMany(item => item.Logs)
                .HasForeignKey(item => item.SourceRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePharmacy(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PharmacyProductFact>(entity =>
        {
            entity.ToTable("pharmacy_product_facts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ActiveIngredient).HasMaxLength(200);
            entity.Property(item => item.DosageForm).HasMaxLength(128);
            entity.Property(item => item.StrengthText).HasMaxLength(128);
            entity.Property(item => item.PackageSize).HasMaxLength(128);
            entity.Property(item => item.Manufacturer).HasMaxLength(200);
            entity.HasIndex(item => item.CanonicalProductId).IsUnique();
            entity.HasOne(item => item.CanonicalProduct)
                .WithOne(item => item.PharmacyProductFact)
                .HasForeignKey<PharmacyProductFact>(item => item.CanonicalProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PharmacyLocation>(entity =>
        {
            entity.ToTable("pharmacy_locations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Address).HasMaxLength(300).IsRequired();
            entity.Property(item => item.City).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Province).HasMaxLength(32);
            entity.Property(item => item.PostalCode).HasMaxLength(16);
            entity.Property(item => item.Latitude).HasPrecision(9, 6);
            entity.Property(item => item.Longitude).HasPrecision(9, 6);
            entity.HasOne(item => item.Source)
                .WithMany(item => item.PharmacyLocations)
                .HasForeignKey(item => item.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PharmacyReservation>(entity =>
        {
            entity.ToTable("pharmacy_reservations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ReservationType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.NreCode).HasMaxLength(64);
            entity.Property(item => item.Status).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.CreatedAtUtc });
            entity.HasOne(item => item.User)
                .WithMany(item => item.PharmacyReservations)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Source)
                .WithMany(item => item.PharmacyReservations)
                .HasForeignKey(item => item.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.CanonicalProduct)
                .WithMany(item => item.PharmacyReservations)
                .HasForeignKey(item => item.CanonicalProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
