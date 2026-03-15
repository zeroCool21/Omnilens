namespace OmnilensScraping.Models;

/// <summary>
/// Informazioni statiche su un retailer supportato.
/// </summary>
public class RetailerInfo
{
    /// <summary>
    /// Identificativo interno del retailer.
    /// </summary>
    public RetailerType Retailer { get; set; }

    /// <summary>
    /// Nome leggibile del retailer.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Host gestiti dal backend per il retailer.
    /// </summary>
    public IReadOnlyCollection<string> Hosts { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Sitemap index principale del retailer.
    /// </summary>
    public string SitemapIndexUrl { get; set; } = string.Empty;

    /// <summary>
    /// Indica se il retailer espone cataloghi prodotto pubblici interrogabili dal backend.
    /// </summary>
    public bool SupportsCatalogDiscovery { get; set; }

    /// <summary>
    /// Nota operativa sulle limitazioni del catalogo pubblico del retailer.
    /// </summary>
    public string? CatalogNotes { get; set; }

    /// <summary>
    /// Stato di copertura del catalogo per il retailer.
    /// </summary>
    public CatalogCoverageStatus CatalogCoverage { get; set; } = new();

    /// <summary>
    /// Modalita di scraping supportate dal retailer.
    /// </summary>
    public IReadOnlyCollection<ScrapingMode> SupportedModes { get; set; } = Array.Empty<ScrapingMode>();
}
