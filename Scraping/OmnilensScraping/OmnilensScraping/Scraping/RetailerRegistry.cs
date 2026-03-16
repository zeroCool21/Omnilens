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
                Category = RetailerCategory.Electronics,
                DisplayName = "Unieuro",
                SitemapIndexUrl = "https://www.unieuro.it/sitemap.xml",
                Hosts = new[] { "unieuro.it", "www.unieuro.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "sitemap-product" }
            },
            [RetailerType.MediaWorld] = new RetailerDefinition
            {
                Retailer = RetailerType.MediaWorld,
                Category = RetailerCategory.Electronics,
                DisplayName = "MediaWorld",
                SitemapIndexUrl = "https://www.mediaworld.it/sitemaps/sitemap-index.xml",
                Hosts = new[] { "mediaworld.it", "www.mediaworld.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "productdetailspages" }
            },
            [RetailerType.Euronics] = new RetailerDefinition
            {
                Retailer = RetailerType.Euronics,
                Category = RetailerCategory.Electronics,
                DisplayName = "Euronics",
                SitemapIndexUrl = "https://www.euronics.it/sitemap_index.xml",
                Hosts = new[] { "euronics.it", "www.euronics.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "product" }
            },
            [RetailerType.AmazonIt] = new RetailerDefinition
            {
                Retailer = RetailerType.AmazonIt,
                Category = RetailerCategory.Marketplace,
                DisplayName = "Amazon IT",
                SitemapIndexUrl = string.Empty,
                Hosts = new[] { "amazon.it", "www.amazon.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = Array.Empty<string>(),
                SupportsCatalogDiscovery = true,
                CatalogNotes = "Amazon IT non espone product sitemap pubbliche complete. Il backend puo costruire una snapshot locale ampia tramite crawl pubblico strutturato e salvarla per categorie, ma la copertura totale al 100% e garantibile solo quando viene configurata una snapshot autorevole completa."
            },
            [RetailerType.Redcare] = new RetailerDefinition
            {
                Retailer = RetailerType.Redcare,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "Redcare",
                SitemapIndexUrl = "https://www.redcare.it/sitemap/sitemap.xml",
                Hosts = new[] { "redcare.it", "www.redcare.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "products-" }
            },
            [RetailerType.DrMax] = new RetailerDefinition
            {
                Retailer = RetailerType.DrMax,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "Dr. Max",
                SitemapIndexUrl = "https://backend.drmax.it/media/sitemap/sitemap.xml",
                Hosts = new[] { "drmax.it", "www.drmax.it", "backend.drmax.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "products_" }
            },
            [RetailerType.Farmasave] = new RetailerDefinition
            {
                Retailer = RetailerType.Farmasave,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "Farmasave",
                SitemapIndexUrl = "https://www.farmasave.it/sitemap/sitemap_farma_sum.xml",
                Hosts = new[] { "farmasave.it", "www.farmasave.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "sitemap_farma" }
            },
            [RetailerType.FarmaciaLoreto] = new RetailerDefinition
            {
                Retailer = RetailerType.FarmaciaLoreto,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "Farmacia Loreto",
                SitemapIndexUrl = "https://farmacialoreto.it/sitemap.xml",
                Hosts = new[] { "farmacialoreto.it", "www.farmacialoreto.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "sitemap" }
            },
            [RetailerType.EFarma] = new RetailerDefinition
            {
                Retailer = RetailerType.EFarma,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "eFarma",
                SitemapIndexUrl = "https://www.efarma.com/media/google_sitemap_index.xml",
                Hosts = new[] { "efarma.com", "www.efarma.com" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "google_sitemap_Prodotti_" }
            },
            [RetailerType.Farmacie1000] = new RetailerDefinition
            {
                Retailer = RetailerType.Farmacie1000,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "1000 Farmacie",
                SitemapIndexUrl = "https://www.1000farmacie.it/sitemaps/sitemap.xml.gz",
                Hosts = new[] { "1000farmacie.it", "www.1000farmacie.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "sitemap" },
                ProductUrlMarkers = new[] { ".html" },
                CatalogNotes = "Il sitemap index pubblico di 1000 Farmacie include anche pagine non prodotto. Il backend filtra gli URL prodotto dal contenuto delle sitemap figlie."
            },
            [RetailerType.TopFarmacia] = new RetailerDefinition
            {
                Retailer = RetailerType.TopFarmacia,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "Top Farmacia",
                SitemapIndexUrl = string.Empty,
                Hosts = new[] { "topfarmacia.it", "www.topfarmacia.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = Array.Empty<string>(),
                SupportsCatalogDiscovery = true,
                CatalogNotes = "Top Farmacia non espone un product sitemap pubblico stabile da usare lato backend. Il catalogo viene costruito da bootstrap crawl pubblico con snapshot locale e richiede modalita Browser per limitare i blocchi.",
                BootstrapSeedUrls = new[]
                {
                    "https://www.topfarmacia.it/",
                    "https://www.topfarmacia.it/sitemap"
                },
                BootstrapDiscoveryPathPrefixes = new[]
                {
                    "/c-",
                    "/sitemap"
                },
                BootstrapExcludedPathPrefixes = DefaultBootstrapExcludedPrefixes,
                BootstrapProductUrlMarkers = new[]
                {
                    "/p-"
                },
                BootstrapUseBrowser = true,
                BootstrapMaxConcurrency = 2,
                BootstrapMaxPages = 2500
            },
            [RetailerType.TuttoFarma] = new RetailerDefinition
            {
                Retailer = RetailerType.TuttoFarma,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "TuttoFarma",
                SitemapIndexUrl = string.Empty,
                Hosts = new[] { "tuttofarma.it", "www.tuttofarma.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = Array.Empty<string>(),
                SupportsCatalogDiscovery = true,
                CatalogNotes = "TuttoFarma non espone un product sitemap XML pubblico stabile. Il backend usa un bootstrap crawl pubblico delle discovery page del sito e ne mantiene una snapshot locale.",
                BootstrapSeedUrls = new[]
                {
                    "https://www.tuttofarma.it/"
                },
                BootstrapExcludedPathPrefixes = DefaultBootstrapExcludedPrefixes,
                BootstrapProductUrlMarkers = new[]
                {
                    ".html"
                },
                BootstrapMaxConcurrency = 4,
                BootstrapMaxPages = 2500
            },
            [RetailerType.FarmaIt] = new RetailerDefinition
            {
                Retailer = RetailerType.FarmaIt,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "Farma.it / Antica Farmacia Orlandi",
                SitemapIndexUrl = "https://www.anticafarmaciaorlandi.it/media/sitemap_index.xml",
                Hosts = new[] { "farma.it", "www.farma.it", "anticafarmaciaorlandi.it", "www.anticafarmaciaorlandi.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "sitemap_" },
                ProductUrlMarkers = new[] { ".html" },
                CatalogNotes = "Il dominio farma.it oggi reindirizza verso Antica Farmacia Orlandi. Il backend usa il sitemap pubblico del dominio finale."
            },
            [RetailerType.BenuFarma] = new RetailerDefinition
            {
                Retailer = RetailerType.BenuFarma,
                Category = RetailerCategory.Pharmacy,
                DisplayName = "BENU Farma",
                SitemapIndexUrl = "https://www.benufarma.it/sitemap.xml",
                Hosts = new[] { "benufarma.it", "www.benufarma.it" },
                SupportedModes = defaultModes,
                ProductSitemapMarkers = new[] { "/prodotti/sitemap/" }
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

    private static readonly string[] DefaultBootstrapExcludedPrefixes =
    {
        "/account",
        "/accedi",
        "/area-riservata",
        "/blog",
        "/brand",
        "/brands",
        "/carrello",
        "/cart",
        "/checkout",
        "/contatti",
        "/cookie",
        "/faq",
        "/help",
        "/informazioni",
        "/login",
        "/magazine",
        "/newsletter",
        "/ordini",
        "/pagine",
        "/privacy",
        "/ricette",
        "/search",
        "/termini",
        "/wishlist"
    };
}
