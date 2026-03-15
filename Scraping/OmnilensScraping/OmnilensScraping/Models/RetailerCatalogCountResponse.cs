namespace OmnilensScraping.Models;

/// <summary>
/// Conteggio dei prodotti presenti nei product sitemap di un retailer.
/// </summary>
public class RetailerCatalogCountResponse
{
    /// <summary>
    /// Retailer interrogato.
    /// </summary>
    public RetailerType Retailer { get; set; }

    /// <summary>
    /// URL della sitemap index del retailer.
    /// </summary>
    public string SitemapIndexUrl { get; set; } = string.Empty;

    /// <summary>
    /// Numero di sitemap prodotto individuate.
    /// </summary>
    public int ProductSitemaps { get; set; }

    /// <summary>
    /// Numero totale di URL prodotto contati.
    /// </summary>
    public int TotalProducts { get; set; }

    /// <summary>
    /// Stato di copertura del catalogo usato per il conteggio.
    /// </summary>
    public CatalogCoverageStatus CatalogCoverage { get; set; } = new();
}
