namespace OmnilensScraping.Persistence.Entities;

public class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RetailerCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "IT";
    public string BaseUrl { get; set; } = string.Empty;
    public bool SupportsCatalogBootstrap { get; set; }
    public bool SupportsLiveScrape { get; set; } = true;
    public bool SupportsApiCollection { get; set; }
    public bool SupportsScrapingCollection { get; set; } = true;
    public bool SupportsManualCollection { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int PriorityScore { get; set; } = 50;
    public decimal HealthScore { get; set; } = 100m;
    public int ConsecutiveFailureCount { get; set; }
    public DateTimeOffset? LastRunAtUtc { get; set; }
    public DateTimeOffset? LastSuccessfulRunAtUtc { get; set; }
    public DateTimeOffset? LastFailedRunAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SourceProduct> SourceProducts { get; set; } = new List<SourceProduct>();
    public ICollection<ClickEvent> ClickEvents { get; set; } = new List<ClickEvent>();
    public ICollection<ConversionEvent> ConversionEvents { get; set; } = new List<ConversionEvent>();
    public ICollection<SourceRun> SourceRuns { get; set; } = new List<SourceRun>();
    public ICollection<PharmacyLocation> PharmacyLocations { get; set; } = new List<PharmacyLocation>();
    public ICollection<PharmacyReservation> PharmacyReservations { get; set; } = new List<PharmacyReservation>();
}

public class CanonicalProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Gtin { get; set; }
    public string? CanonicalSku { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string Vertical { get; set; } = "Generic";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CanonicalProductAttribute> Attributes { get; set; } = new List<CanonicalProductAttribute>();
    public ICollection<SourceProduct> SourceProducts { get; set; } = new List<SourceProduct>();
    public ICollection<WishlistEntry> WishlistEntries { get; set; } = new List<WishlistEntry>();
    public ICollection<AlertRule> AlertRules { get; set; } = new List<AlertRule>();
    public ICollection<ClickEvent> ClickEvents { get; set; } = new List<ClickEvent>();
    public ICollection<PharmacyReservation> PharmacyReservations { get; set; } = new List<PharmacyReservation>();
    public PharmacyProductFact? PharmacyProductFact { get; set; }
}

public class CanonicalProductAttribute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CanonicalProductId { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public string AttributeValue { get; set; } = string.Empty;

    public CanonicalProduct CanonicalProduct { get; set; } = null!;
}

public class SourceProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public Guid? CanonicalProductId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string? SourceProductKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Sku { get; set; }
    public string? Gtin { get; set; }
    public string? Currency { get; set; }
    public string? AvailabilityText { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset LastScrapedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSuccessAtUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public Source Source { get; set; } = null!;
    public CanonicalProduct? CanonicalProduct { get; set; }
    public ICollection<ProductOffer> ProductOffers { get; set; } = new List<ProductOffer>();
    public ICollection<PriceHistoryEntry> PriceHistoryEntries { get; set; } = new List<PriceHistoryEntry>();
}

public class ProductOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceProductId { get; set; }
    public decimal? Price { get; set; }
    public string? PriceText { get; set; }
    public string? Currency { get; set; }
    public string? AvailabilityText { get; set; }
    public string? StockStatus { get; set; }
    public string? ShippingText { get; set; }
    public string OfferUrl { get; set; } = string.Empty;
    public DateTimeOffset ScrapedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsLatest { get; set; } = true;

    public SourceProduct SourceProduct { get; set; } = null!;
}

public class PriceHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceProductId { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? AvailabilityText { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public SourceProduct SourceProduct { get; set; } = null!;
}
