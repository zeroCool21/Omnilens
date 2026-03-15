namespace OmnilensScraping.Scraping;

public class AmazonCatalogOptions
{
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
    public List<string> BootstrapSeedUrls { get; set; } = new();
    public string BootstrapFilePrefix { get; set; } = "amazon-it-bootstrap-product";
    public int CatalogFileReadConcurrency { get; set; } = Math.Min(16, Math.Max(4, Environment.ProcessorCount));
}
