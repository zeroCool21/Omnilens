using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class RetailerDefinition
{
    public RetailerType Retailer { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public RetailerCategory Category { get; init; } = RetailerCategory.Electronics;
    public string SitemapIndexUrl { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Hosts { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<ScrapingMode> SupportedModes { get; init; } = Array.Empty<ScrapingMode>();
    public IReadOnlyCollection<string> ProductSitemapMarkers { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> ProductUrlMarkers { get; init; } = Array.Empty<string>();
    public bool SupportsCatalogDiscovery { get; init; } = true;
    public string? CatalogNotes { get; init; }
    public IReadOnlyCollection<string> BootstrapSeedUrls { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> BootstrapDiscoveryPathPrefixes { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> BootstrapExcludedPathPrefixes { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> BootstrapProductUrlMarkers { get; init; } = Array.Empty<string>();
    public bool BootstrapUseBrowser { get; init; }
    public int? BootstrapMaxConcurrency { get; init; }
    public int? BootstrapMaxPages { get; init; }
}
