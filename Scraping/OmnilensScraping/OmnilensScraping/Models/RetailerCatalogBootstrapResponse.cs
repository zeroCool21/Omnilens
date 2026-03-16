namespace OmnilensScraping.Models;

/// <summary>
/// Esito della generazione automatica di una snapshot catalogo locale.
/// </summary>
public class RetailerCatalogBootstrapResponse
{
    public bool Success { get; set; }
    public RetailerType Retailer { get; set; }
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
