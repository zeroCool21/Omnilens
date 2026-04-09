namespace OmnilensScraping.Models;

public sealed class B2bBenchmarkCurrentResponse
{
    public int Total { get; set; }
    public IReadOnlyCollection<B2bBenchmarkProductRow> Items { get; set; } = Array.Empty<B2bBenchmarkProductRow>();
}

public sealed class B2bBenchmarkProductRow
{
    public Guid CanonicalProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Vertical { get; set; } = string.Empty;
    public decimal? BestPrice { get; set; }
    public decimal? WorstPrice { get; set; }
    public decimal? AveragePrice { get; set; }
    public decimal? PriceSpread { get; set; }
    public int OfferCount { get; set; }
    public int CompetitorCount { get; set; }
    public IReadOnlyCollection<B2bBenchmarkOfferRow> Offers { get; set; } = Array.Empty<B2bBenchmarkOfferRow>();
}

public sealed class B2bBenchmarkOfferRow
{
    public string RetailerCode { get; set; } = string.Empty;
    public string RetailerName { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? AvailabilityText { get; set; }
    public DateTimeOffset ScrapedAtUtc { get; set; }
}

public sealed class B2bBenchmarkTrendResponse
{
    public int Days { get; set; }
    public IReadOnlyCollection<B2bBenchmarkTrendRow> Items { get; set; } = Array.Empty<B2bBenchmarkTrendRow>();
}

public sealed class B2bBenchmarkTrendRow
{
    public Guid CanonicalProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string RetailerCode { get; set; } = string.Empty;
    public decimal? LatestPrice { get; set; }
    public decimal? MinimumPrice { get; set; }
    public decimal? MaximumPrice { get; set; }
    public decimal? AbsoluteChange { get; set; }
    public decimal? PercentChange { get; set; }
    public int Samples { get; set; }
    public DateTimeOffset LatestRecordedAtUtc { get; set; }
}
