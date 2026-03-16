namespace OmnilensScraping.Scraping;

public class CatalogBootstrapOptions
{
    public int MaxPages { get; set; } = 2500;
    public int MaxConcurrency { get; set; } = Math.Min(8, Math.Max(2, Environment.ProcessorCount / 2));
    public int MaxRetryAttempts { get; set; } = 5;
    public int RetryBaseDelayMs { get; set; } = 600;
    public int RetryJitterMaxMs { get; set; } = 1200;
    public int RequestMinDelayMs { get; set; } = 100;
    public int RequestMaxDelayMs { get; set; } = 600;
}
