using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class RetailerRegistry
{
    private readonly Dictionary<RetailerType, RetailerDefinition> _definitions;

    public RetailerRegistry()
    {
        var defaultModes = new[]
        {
            ScrapingMode.Auto,
            ScrapingMode.Html,
            ScrapingMode.JsonLd,
            ScrapingMode.EmbeddedState,
            ScrapingMode.Browser
        };

        _definitions = new Dictionary<RetailerType, RetailerDefinition>
        {
            [RetailerType.Unieuro] = new RetailerDefinition
            {
                Retailer = RetailerType.Unieuro,
                DisplayName = "Unieuro",
                SitemapIndexUrl = "https://www.unieuro.it/sitemap.xml",
                Hosts = new[] { "unieuro.it", "www.unieuro.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "sitemap-product" }
            },
            [RetailerType.MediaWorld] = new RetailerDefinition
            {
                Retailer = RetailerType.MediaWorld,
                DisplayName = "MediaWorld",
                SitemapIndexUrl = "https://www.mediaworld.it/sitemaps/sitemap-index.xml",
                Hosts = new[] { "mediaworld.it", "www.mediaworld.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "productdetailspages" }
            },
            [RetailerType.Euronics] = new RetailerDefinition
            {
                Retailer = RetailerType.Euronics,
                DisplayName = "Euronics",
                SitemapIndexUrl = "https://www.euronics.it/sitemap_index.xml",
                Hosts = new[] { "euronics.it", "www.euronics.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "product" }
            },
            [RetailerType.AmazonIt] = new RetailerDefinition
            {
                Retailer = RetailerType.AmazonIt,
                DisplayName = "Amazon IT",
                SitemapIndexUrl = string.Empty,
                Hosts = new[] { "amazon.it", "www.amazon.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = Array.Empty<string>(),
                SupportsCatalogDiscovery = true,
                CatalogNotes = "Amazon IT non espone product sitemap pubbliche complete. Il backend puo creare automaticamente una prima snapshot locale crawlando le pagine bestseller pubbliche e salvando sitemap XML locali, ma la copertura resta best-effort e non dimostra esaustivita dell'intero marketplace."
            }
        };
    }

    public IReadOnlyCollection<RetailerDefinition> GetDefinitions()
    {
        return _definitions.Values
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public RetailerDefinition Get(RetailerType retailer)
    {
        if (_definitions.TryGetValue(retailer, out var definition))
        {
            return definition;
        }

        throw new ArgumentOutOfRangeException(nameof(retailer), retailer, "Retailer non supportato.");
    }

    public bool TryResolve(Uri url, out RetailerDefinition? definition)
    {
        definition = _definitions.Values.FirstOrDefault(candidate =>
            candidate.Hosts.Any(host => url.Host.Contains(host, StringComparison.OrdinalIgnoreCase)));

        return definition is not null;
    }
}
