namespace OmnilensScraping.Scraping;

public class AmazonCatalogOptions
{
    public string AuthoritativeCatalogDirectory { get; set; } = string.Empty;
    public List<string> AuthoritativeFilePaths { get; set; } = new();
    public List<string> AuthoritativeFilePatterns { get; set; } = new()
    {
        "*.txt",
        "*.csv",
        "*.tsv",
        "*.xml",
        "*.json",
        "*.jsonl"
    };
    public string CatalogDirectory { get; set; } = string.Empty;
    public List<string> FilePaths { get; set; } = new();
    public List<string> FilePatterns { get; set; } = new()
    {
        "*.txt",
        "*.csv",
        "*.tsv",
        "*.xml",
        "*.json",
        "*.jsonl"
    };
    public bool AutoBootstrapIfMissing { get; set; } = true;
    public int BootstrapMaxPages { get; set; } = 2500;
    public int BootstrapMaxConcurrency { get; set; } = Math.Min(16, Math.Max(4, Environment.ProcessorCount));
    public bool BootstrapUseBrowser { get; set; }
    public int BootstrapMaxRetryAttempts { get; set; } = 6;
    public int BootstrapRetryBaseDelayMs { get; set; } = 750;
    public int BootstrapRetryJitterMaxMs { get; set; } = 1500;
    public int BootstrapRequestMinDelayMs { get; set; } = 150;
    public int BootstrapRequestMaxDelayMs { get; set; } = 900;
    public List<string> BootstrapSeedUrls { get; set; } = new();
    public List<string> BootstrapSearchAliases { get; set; } = new();
    public string BootstrapFilePrefix { get; set; } = "amazon-it-bootstrap-product";
    public int CatalogFileReadConcurrency { get; set; } = Math.Min(16, Math.Max(4, Environment.ProcessorCount));
}
