using HtmlAgilityPack;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class GenericStructuredRetailerScraper : RetailerScraperBase
{
    private static readonly string[] TitleXpaths =
    {
        "//*[@itemprop='name']",
        "//h1",
        "//*[contains(@class,'product-name')]",
        "//*[contains(@class,'product-title')]"
    };

    private static readonly string[] PriceXpaths =
    {
        "//*[@itemprop='price']",
        "//*[contains(@class,'special-price')]",
        "//*[contains(@class,'sales-price')]",
        "//*[contains(@class,'final-price')]",
        "//*[contains(@class,'product-price')]",
        "//*[contains(@class,'price')]//*[contains(@class,'amount')]",
        "//*[contains(@class,'price')]"
    };

    private static readonly string[] DescriptionXpaths =
    {
        "//*[@itemprop='description']",
        "//*[@id='product-description']",
        "//*[@id='description']",
        "//*[contains(@class,'product-description')]",
        "//*[contains(@class,'description')]"
    };

    private static readonly string[] AvailabilityXpaths =
    {
        "//*[@itemprop='availability']",
        "//*[contains(@class,'availability')]",
        "//*[contains(@class,'stock')]",
        "//*[contains(@class,'disponibil')]"
    };

    private readonly RetailerType _retailer;
    private readonly string[] _hosts;
    private readonly string _warningPrefix;

    public GenericStructuredRetailerScraper(
        PageContentService pageContentService,
        RetailerType retailer,
        IEnumerable<string> hosts,
        string displayName)
        : base(pageContentService)
    {
        _retailer = retailer;
        _hosts = hosts
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .ToArray();
        _warningPrefix = displayName;
    }

    public override RetailerType Retailer => _retailer;

    public override bool CanHandle(Uri url)
    {
        return _hosts.Any(host => url.Host.Contains(host, StringComparison.OrdinalIgnoreCase));
    }

    public override async Task<ScraperExecutionResult> ScrapeAsync(Uri url, ScrapingMode mode, CancellationToken cancellationToken)
    {
        var useBrowser = mode == ScrapingMode.Browser;
        var html = await PageContentService.GetHtmlAsync(url, useBrowser, cancellationToken);
        var document = HtmlHelpers.Load(html);
        var product = CreateProduct(url, mode);
        var warnings = new List<string>();
        var strategies = new List<string>();
        var appliedMode = mode;

        var metaProduct = ParseMeta(document, url);
        ApplyFrom(metaProduct, product);
        if (HasAnyData(metaProduct))
        {
            strategies.Add("meta");
        }

        if (mode is ScrapingMode.JsonLd or ScrapingMode.Auto or ScrapingMode.Browser or ScrapingMode.EmbeddedState)
        {
            var jsonLdProduct = StructuredDataParser.TryParseJsonLd(html, url);
            if (jsonLdProduct is not null)
            {
                ApplyFrom(jsonLdProduct, product);
                strategies.Add("json-ld");
                if (mode is ScrapingMode.Auto or ScrapingMode.EmbeddedState)
                {
                    appliedMode = ScrapingMode.JsonLd;
                }
            }
        }

        if (mode is ScrapingMode.Html or ScrapingMode.Auto or ScrapingMode.Browser or ScrapingMode.EmbeddedState)
        {
            var htmlProduct = ParseHtml(document, url);
            ApplyFrom(htmlProduct, product);
            strategies.Add("html");
            if (mode == ScrapingMode.Auto && string.IsNullOrWhiteSpace(product.Title) && !product.Price.HasValue)
            {
                appliedMode = ScrapingMode.Html;
            }
        }

        if (string.IsNullOrWhiteSpace(product.Title))
        {
            warnings.Add($"{_warningPrefix}: titolo non trovato in modo affidabile. Prova la modalita Browser se la pagina espone challenge o rendering client-side.");
        }

        if (!product.Price.HasValue)
        {
            warnings.Add($"{_warningPrefix}: prezzo non trovato nei dati strutturati o nell'HTML principale.");
        }

        return new ScraperExecutionResult
        {
            Product = product,
            AppliedMode = appliedMode,
            AppliedStrategies = strategies
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Warnings = warnings
        };
    }

    private static ProductData ParseMeta(HtmlDocument document, Uri url)
    {
        var product = new ProductData { Url = url.ToString() };
        product.Title = HtmlHelpers.GetMetaContent(document, "og:title") ??
                        HtmlHelpers.GetMetaContent(document, "twitter:title") ??
                        HtmlHelpers.NormalizeText(document.DocumentNode.SelectSingleNode("//title")?.InnerText);
        product.Description = HtmlHelpers.GetMetaContent(document, "description") ??
                              HtmlHelpers.GetMetaContent(document, "og:description") ??
                              HtmlHelpers.GetMetaContent(document, "twitter:description");

        var imageUrl = HtmlHelpers.MakeAbsolute(url,
            HtmlHelpers.GetMetaContent(document, "og:image") ??
            HtmlHelpers.GetMetaContent(document, "twitter:image"));

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            product.ImageUrl = imageUrl;
            product.Images.Add(imageUrl);
        }

        product.PriceText = ReadFirstAttribute(document, "content",
                               "//meta[@property='product:price:amount']",
                               "//meta[@property='og:price:amount']",
                               "//meta[@name='twitter:data1']") ??
                            ReadFirstAttribute(document, "content",
                               "//meta[@itemprop='price']");
        product.Price = HtmlHelpers.ParsePrice(product.PriceText);
        product.Currency = ReadFirstAttribute(document, "content",
                               "//meta[@property='product:price:currency']",
                               "//meta[@property='og:price:currency']",
                               "//meta[@itemprop='priceCurrency']");
        product.Brand = ReadFirstAttribute(document, "content",
                            "//meta[@name='brand']",
                            "//meta[@property='product:brand']");
        product.Availability = HtmlHelpers.NormalizeAvailability(
            ReadFirstAttribute(document, "content",
                "//link[@itemprop='availability']",
                "//meta[@itemprop='availability']"));
        return product;
    }

    private static ProductData ParseHtml(HtmlDocument document, Uri url)
    {
        var product = new ProductData { Url = url.ToString() };
        product.Title = ReadFirstText(document, TitleXpaths) ??
                        HtmlHelpers.NormalizeText(document.DocumentNode.SelectSingleNode("//title")?.InnerText);
        product.Brand = ReadFirstText(document,
                           "//*[@itemprop='brand']",
                           "//*[contains(@class,'brand')]",
                           "//*[contains(text(),'Marca')]/following-sibling::*[1]") ??
                        ReadFirstAttribute(document, "content", "//meta[@name='brand']");

        product.Sku = ReadFirstAttribute(document, "content",
                          "//meta[@itemprop='sku']",
                          "//meta[@itemprop='mpn']") ??
                      ReadFirstAttribute(document, "value",
                          "//*[@name='sku']",
                          "//*[@name='product_sku']",
                          "//*[@id='sku']",
                          "//*[@id='product-sku']") ??
                      ReadFirstText(document,
                          "//*[@itemprop='sku']",
                          "//*[contains(@class,'sku')]",
                          "//*[contains(text(),'SKU')]/following-sibling::*[1]");

        product.Gtin = ReadFirstAttribute(document, "content",
                           "//meta[@itemprop='gtin13']",
                           "//meta[@itemprop='gtin']",
                           "//meta[@itemprop='ean']") ??
                       ReadFirstText(document,
                           "//*[@itemprop='gtin13']",
                           "//*[@itemprop='gtin']",
                           "//*[contains(text(),'EAN')]/following-sibling::*[1]");

        product.PriceText = ReadFirstAttribute(document, "content",
                               "//*[@itemprop='price']",
                               "//meta[@itemprop='price']") ??
                            ReadFirstText(document, PriceXpaths);
        product.Price = HtmlHelpers.ParsePrice(product.PriceText);
        product.Currency = ReadFirstAttribute(document, "content", "//meta[@itemprop='priceCurrency']") ??
                           (product.PriceText?.Contains('€') == true ? "EUR" : null);

        product.Availability = HtmlHelpers.NormalizeAvailability(
            ReadFirstAttribute(document, "href", "//link[@itemprop='availability']") ??
            ReadFirstText(document, AvailabilityXpaths));

        product.Description = ReadFirstText(document, DescriptionXpaths) ??
                              HtmlHelpers.GetMetaContent(document, "description");

        foreach (var imageUrl in ReadImageCandidates(document, url))
        {
            if (!product.Images.Contains(imageUrl, StringComparer.OrdinalIgnoreCase))
            {
                product.Images.Add(imageUrl);
            }
        }

        product.ImageUrl ??= product.Images.FirstOrDefault();
        PopulateSpecificationTables(document, product);
        return product;
    }

    private static IEnumerable<string> ReadImageCandidates(HtmlDocument document, Uri url)
    {
        var sources = document.DocumentNode
            .SelectNodes("//img[@src or @data-src or @data-original]")
            ?.Select(node =>
                HtmlHelpers.MakeAbsolute(url,
                    node.GetAttributeValue("data-original", null) ??
                    node.GetAttributeValue("data-src", null) ??
                    node.GetAttributeValue("src", null)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray() ?? Array.Empty<string>();

        foreach (var source in sources)
        {
            yield return source;
        }
    }

    private static void PopulateSpecificationTables(HtmlDocument document, ProductData product)
    {
        foreach (var row in document.DocumentNode.SelectNodes("//table//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var headerCell = row.SelectSingleNode("./th") ?? row.SelectSingleNode("./td[1]");
            var valueCell = row.SelectSingleNode("./td[last()]");
            HtmlHelpers.AddSpecification(product.Specifications, headerCell?.InnerText, valueCell?.InnerText);
        }

        foreach (var definitionList in document.DocumentNode.SelectNodes("//dl[dt and dd]") ?? Enumerable.Empty<HtmlNode>())
        {
            var keys = definitionList.SelectNodes("./dt") ?? Enumerable.Empty<HtmlNode>();
            var values = definitionList.SelectNodes("./dd") ?? Enumerable.Empty<HtmlNode>();
            foreach (var pair in keys.Zip(values, (key, value) => new { key, value }))
            {
                HtmlHelpers.AddSpecification(product.Specifications, pair.key.InnerText, pair.value.InnerText);
            }
        }
    }

    private static string? ReadFirstText(HtmlDocument document, params string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var value = HtmlHelpers.GetTextByXPath(document, xpath);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadFirstAttribute(HtmlDocument document, string attributeName, params string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var node = document.DocumentNode.SelectSingleNode(xpath);
            var value = HtmlHelpers.NormalizeText(node?.GetAttributeValue(attributeName, null));
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool HasAnyData(ProductData product)
    {
        return !string.IsNullOrWhiteSpace(product.Title) ||
               !string.IsNullOrWhiteSpace(product.Brand) ||
               !string.IsNullOrWhiteSpace(product.Sku) ||
               !string.IsNullOrWhiteSpace(product.Gtin) ||
               !string.IsNullOrWhiteSpace(product.Description) ||
               !string.IsNullOrWhiteSpace(product.ImageUrl) ||
               product.Price.HasValue ||
               product.Images.Count > 0;
    }
}
