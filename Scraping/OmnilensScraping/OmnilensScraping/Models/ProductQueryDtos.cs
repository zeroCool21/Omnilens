namespace OmnilensScraping.Models;

public sealed class ProductSearchResponse
{
    public IReadOnlyCollection<ProductSearchItemResponse> Items { get; set; } = Array.Empty<ProductSearchItemResponse>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public sealed class ProductSearchItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Vertical { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal? BestPrice { get; set; }
    public string? Currency { get; set; }
    public int SourceCount { get; set; }
    public bool IsWishlisted { get; set; }
}

public sealed class ProductDetailResponse
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Vertical { get; set; } = string.Empty;
    public string? Gtin { get; set; }
    public string? CanonicalSku { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public decimal? BestPrice { get; set; }
    public string? Currency { get; set; }
    public bool IsWishlisted { get; set; }
    public IReadOnlyDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
}

public sealed class ProductOfferResponse
{
    public Guid OfferId { get; set; }
    public Guid SourceId { get; set; }
    public string RetailerCode { get; set; } = string.Empty;
    public string RetailerName { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? PriceText { get; set; }
    public string? Currency { get; set; }
    public string? AvailabilityText { get; set; }
    public string? StockStatus { get; set; }
    public string OfferUrl { get; set; } = string.Empty;
    public DateTimeOffset ScrapedAtUtc { get; set; }
}

public sealed class ProductPriceHistoryPointResponse
{
    public Guid SourceProductId { get; set; }
    public string RetailerCode { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? AvailabilityText { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
