namespace OmnilensScraping.Models;

public sealed class SourceAdminItemResponse
{
    public Guid Id { get; set; }
    public string RetailerCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool SupportsCatalogBootstrap { get; set; }
    public bool SupportsLiveScrape { get; set; }
    public bool SupportsApiCollection { get; set; }
    public bool SupportsScrapingCollection { get; set; }
    public bool SupportsManualCollection { get; set; }
    public bool IsEnabled { get; set; }
    public int PriorityScore { get; set; }
    public decimal HealthScore { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public DateTimeOffset? LastRunAtUtc { get; set; }
    public DateTimeOffset? LastSuccessfulRunAtUtc { get; set; }
    public DateTimeOffset? LastFailedRunAtUtc { get; set; }
    public int TotalRuns { get; set; }
    public int SuccessfulRuns { get; set; }
    public int FailedRuns { get; set; }
}

public sealed class UpdateSourceAdminRequest
{
    public bool? SupportsApiCollection { get; set; }
    public bool? SupportsScrapingCollection { get; set; }
    public bool? SupportsManualCollection { get; set; }
    public bool? IsEnabled { get; set; }
    public int? PriorityScore { get; set; }
}

public sealed class SourceHealthSummaryResponse
{
    public int TotalSources { get; set; }
    public int HealthySources { get; set; }
    public int DegradedSources { get; set; }
    public int UnhealthySources { get; set; }
    public int DisabledSources { get; set; }
}
