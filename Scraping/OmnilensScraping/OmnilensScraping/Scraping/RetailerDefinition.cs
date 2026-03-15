using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class RetailerDefinition
{
    public RetailerType Retailer { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string SitemapIndexUrl { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Hosts { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<ScrapingMode> SupportedModes { get; init; } = Array.Empty<ScrapingMode>();
    public IReadOnlyCollection<string> ProductSitemapMarkers { get; init; } = Array.Empty<string>();
    public bool SupportsCatalogDiscovery { get; init; } = true;
    public string? CatalogNotes { get; init; }
}
