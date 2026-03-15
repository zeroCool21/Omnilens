namespace OmnilensScraping.Scraping;

public class ScrapingOptions
{
    public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

    public string UserAgent { get; set; } = DefaultUserAgent;
    public int BrowserTimeoutMs { get; set; } = 60000;
    public int MaxBatchConcurrency { get; set; } = Math.Min(32, Math.Max(8, Environment.ProcessorCount * 2));
    public int MaxHttpConcurrency { get; set; } = Math.Min(64, Math.Max(12, Environment.ProcessorCount * 4));
    public int MaxBrowserConcurrency { get; set; } = Math.Min(8, Math.Max(2, Environment.ProcessorCount / 2));
    public int MaxSitemapConcurrency { get; set; } = Math.Min(16, Math.Max(4, Environment.ProcessorCount));
}
