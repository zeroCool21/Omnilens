namespace OmnilensScraping.Models;

/// <summary>
/// Esito della generazione automatica della prima snapshot catalogo Amazon IT.
/// </summary>
public class AmazonCatalogBootstrapResponse
{
    public bool Success { get; set; }
    public RetailerType Retailer { get; set; } = RetailerType.AmazonIt;
    public string OutputDirectory { get; set; } = string.Empty;
    public int CrawledPages { get; set; }
    public int EnqueuedPages { get; set; }
    public int GeneratedSitemaps { get; set; }
    public int DiscoveredProducts { get; set; }
    public int PersistedProducts { get; set; }
    public int DiscoveredCategories { get; set; }
    public int? RequestedTake { get; set; }
    public long DurationMs { get; set; }
    public IReadOnlyCollection<string> SitemapFiles { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Warnings { get; set; } = Array.Empty<string>();
}
