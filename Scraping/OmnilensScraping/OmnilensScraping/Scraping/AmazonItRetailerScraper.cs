using System.Text.Json.Nodes;
using HtmlAgilityPack;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class AmazonItRetailerScraper : RetailerScraperBase
{
    private static readonly string[] PriceXpaths =
    {
        "//*[@id='corePrice_feature_div']//span[contains(@class,'a-price')]/span[contains(@class,'a-offscreen')]",
        "//*[@id='corePrice_desktop']//span[contains(@class,'a-price')]/span[contains(@class,'a-offscreen')]",
        "//*[@id='corePriceDisplay_desktop_feature_div']//span[contains(@class,'a-price')]/span[contains(@class,'a-offscreen')]",
        "//*[@id='apex_desktop']//span[contains(@class,'a-price')]/span[contains(@class,'a-offscreen')]",
        "//*[@id='price_inside_buybox']",
        "//*[@id='priceblock_ourprice']",
        "//*[@id='priceblock_dealprice']",
        "//*[@id='priceblock_saleprice']",
        "(//*[@id='tp_price_block_total_price_ww']//span[contains(@class,'a-offscreen')])[1]"
    };

    public AmazonItRetailerScraper(PageContentService pageContentService)
        : base(pageContentService)
    {
    }

    public override RetailerType Retailer => RetailerType.AmazonIt;

    public override bool CanHandle(Uri url)
    {
        return url.Host.Contains("amazon.it", StringComparison.OrdinalIgnoreCase);
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
                if (mode == ScrapingMode.Auto || mode == ScrapingMode.EmbeddedState)
                {
                    appliedMode = ScrapingMode.JsonLd;
                }
            }
        }

        if (mode is ScrapingMode.EmbeddedState or ScrapingMode.Auto or ScrapingMode.Browser)
        {
            var embeddedProduct = ParseEmbeddedState(document, url);
            if (embeddedProduct is not null)
            {
                ApplyFrom(embeddedProduct, product);
                strategies.Add("embedded-state");
                if (mode == ScrapingMode.EmbeddedState)
                {
                    appliedMode = ScrapingMode.EmbeddedState;
                }
            }
        }

        if (mode is ScrapingMode.Html or ScrapingMode.Auto or ScrapingMode.Browser or ScrapingMode.EmbeddedState)
        {
            var htmlProduct = ParseHtml(document, url);
            ApplyFrom(htmlProduct, product);
            strategies.Add("html");
        }

        if (!product.Price.HasValue)
        {
            warnings.Add("Amazon IT puo nascondere il prezzo dietro login, CAP di consegna, selezione variante o challenge anti-bot. Se i dati sono parziali prova la modalita Browser su un URL canonico /dp/{ASIN}.");
        }

        if (string.IsNullOrWhiteSpace(product.Title))
        {
            warnings.Add("La pagina Amazon IT non ha esposto un titolo affidabile al parser: verifica che l'URL punti a una scheda prodotto reale e non a una redirect o a una pagina con challenge.");
        }

        return new ScraperExecutionResult
        {
            Product = product,
            AppliedMode = appliedMode,
            AppliedStrategies = strategies,
            Warnings = warnings
        };
    }

    private static ProductData ParseMeta(HtmlDocument document, Uri url)
    {
        var product = new ProductData { Url = url.ToString() };
        product.Title = HtmlHelpers.GetMetaContent(document, "og:title") ??
                        HtmlHelpers.NormalizeText(document.DocumentNode.SelectSingleNode("//title")?.InnerText);
        product.Description = HtmlHelpers.GetMetaContent(document, "description") ??
                              HtmlHelpers.GetMetaContent(document, "og:description");

        var imageUrl = HtmlHelpers.MakeAbsolute(url, HtmlHelpers.GetMetaContent(document, "og:image"));
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            product.ImageUrl = imageUrl;
            product.Images.Add(imageUrl);
        }

        product.Currency = HtmlHelpers.GetMetaContent(document, "og:price:currency");
        return product;
    }

    private static ProductData? ParseEmbeddedState(HtmlDocument document, Uri url)
    {
        var product = new ProductData { Url = url.ToString() };
        product.Sku = ReadFirstAttribute(
            document,
            "value",
            "//*[@id='ASIN']",
            "//*[@name='ASIN']");

        var imageNode = document.DocumentNode.SelectSingleNode("//*[@id='landingImage']");
        var dynamicImageJson = imageNode?.GetAttributeValue("data-a-dynamic-image", null);
        if (!string.IsNullOrWhiteSpace(dynamicImageJson))
        {
            try
            {
                if (JsonNode.Parse(dynamicImageJson) is JsonObject imageMap)
                {
                    foreach (var imageUrl in imageMap.Select(entry => HtmlHelpers.MakeAbsolute(url, entry.Key)))
                    {
                        if (!string.IsNullOrWhiteSpace(imageUrl) &&
                            !product.Images.Contains(imageUrl, StringComparer.OrdinalIgnoreCase))
                        {
                            product.Images.Add(imageUrl);
                        }
                    }

                    product.ImageUrl = product.Images.FirstOrDefault();
                }
            }
            catch
            {
                // Ignora JSON malformato e continua con i fallback HTML.
            }
        }

        var oldHires = HtmlHelpers.MakeAbsolute(url, imageNode?.GetAttributeValue("data-old-hires", null));
        if (!string.IsNullOrWhiteSpace(oldHires))
        {
            product.ImageUrl ??= oldHires;
            if (!product.Images.Contains(oldHires, StringComparer.OrdinalIgnoreCase))
            {
                product.Images.Add(oldHires);
            }
        }

        return HasAnyData(product) ? product : null;
    }

    private static ProductData ParseHtml(HtmlDocument document, Uri url)
    {
        var product = new ProductData { Url = url.ToString() };
        product.Title = HtmlHelpers.GetTextByXPath(document, "//*[@id='productTitle']");
        product.Brand = NormalizeBrand(HtmlHelpers.GetTextByXPath(document, "//*[@id='bylineInfo']"));
        product.Sku = ReadFirstAttribute(
            document,
            "value",
            "//*[@id='ASIN']",
            "//*[@name='ASIN']");

        product.PriceText = ReadFirstText(document, PriceXpaths);
        product.Price = HtmlHelpers.ParsePrice(product.PriceText);
        product.Currency = product.PriceText?.Contains('€') == true ? "EUR" : product.Currency;
        product.Availability = HtmlHelpers.GetTextByXPath(document, "//*[@id='availability']//span") ??
                               HtmlHelpers.GetTextByXPath(document, "//*[@id='availability']");

        product.Description = ReadBulletDescription(document) ??
                              HtmlHelpers.GetTextByXPath(document, "//*[@id='productDescription']");

        var landingImage = document.DocumentNode.SelectSingleNode("//*[@id='landingImage']");
        foreach (var imageUrl in ReadImageCandidates(url, landingImage))
        {
            if (!product.Images.Contains(imageUrl, StringComparer.OrdinalIgnoreCase))
            {
                product.Images.Add(imageUrl);
            }
        }

        product.ImageUrl ??= product.Images.FirstOrDefault();

        PopulateSpecificationTables(document, product);
        PopulateDetailBullets(document, product);

        product.Brand ??= GetSpecificationValue(product.Specifications, "Marca", "Brand");
        product.Sku ??= GetSpecificationValue(product.Specifications, "ASIN");
        product.Gtin ??= GetSpecificationValue(product.Specifications, "EAN", "UPC", "ISBN-13", "Codice EAN");

        return product;
    }

    private static IEnumerable<string> ReadImageCandidates(Uri url, HtmlNode? imageNode)
    {
        if (imageNode is null)
        {
            yield break;
        }

        var oldHires = HtmlHelpers.MakeAbsolute(url, imageNode.GetAttributeValue("data-old-hires", null));
        if (!string.IsNullOrWhiteSpace(oldHires))
        {
            yield return oldHires;
        }

        var source = HtmlHelpers.MakeAbsolute(url, imageNode.GetAttributeValue("src", null));
        if (!string.IsNullOrWhiteSpace(source))
        {
            yield return source;
        }
    }

    private static void PopulateSpecificationTables(HtmlDocument document, ProductData product)
    {
        var tableXpaths = new[]
        {
            "//*[@id='productDetails_techSpec_section_1']//tr[th and td]",
            "//*[@id='productDetails_detailBullets_sections1']//tr[th and td]",
            "//*[@id='technicalSpecifications_section_1']//tr[th and td]"
        };

        foreach (var tableXpath in tableXpaths)
        {
            foreach (var row in document.DocumentNode.SelectNodes(tableXpath) ?? Enumerable.Empty<HtmlNode>())
            {
                var key = row.SelectSingleNode("./th|./td[1]")?.InnerText;
                var value = row.SelectSingleNode("./td[last()]")?.InnerText;
                HtmlHelpers.AddSpecification(product.Specifications, key, value, "Dettagli prodotto");
            }
        }
    }

    private static void PopulateDetailBullets(HtmlDocument document, ProductData product)
    {
        foreach (var item in document.DocumentNode.SelectNodes("//*[@id='detailBullets_feature_div']//li") ?? Enumerable.Empty<HtmlNode>())
        {
            var labelNode = item.SelectSingleNode(".//*[contains(@class,'a-text-bold')]");
            var key = HtmlHelpers.NormalizeText(labelNode?.InnerText)?.TrimEnd(':');
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var valueParts = item.SelectNodes(".//span")
                ?.Select(node => HtmlHelpers.NormalizeText(node.InnerText))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToList() ?? new List<string>();

            if (valueParts.Count == 0)
            {
                continue;
            }

            if (string.Equals(valueParts[0]?.TrimEnd(':'), key, StringComparison.OrdinalIgnoreCase))
            {
                valueParts.RemoveAt(0);
            }

            HtmlHelpers.AddSpecification(product.Specifications, key, string.Join(" ", valueParts), "Dettagli prodotto");
        }
    }

    private static string? ReadBulletDescription(HtmlDocument document)
    {
        var bullets = (document.DocumentNode.SelectNodes("//*[@id='feature-bullets']//span[contains(@class,'a-list-item')]")
                      ?? Enumerable.Empty<HtmlNode>())
            .Select(node => HtmlHelpers.NormalizeText(node.InnerText))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return bullets.Length == 0 ? null : string.Join(" ", bullets);
    }

    private static string? ReadFirstText(HtmlDocument document, IEnumerable<string> xpaths)
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

    private static string? GetSpecificationValue(IReadOnlyDictionary<string, string> specifications, params string[] keyFragments)
    {
        foreach (var fragment in keyFragments)
        {
            var match = specifications.FirstOrDefault(entry =>
                entry.Key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }

    private static string? NormalizeBrand(string? brand)
    {
        var normalized = HtmlHelpers.NormalizeText(brand);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized
            .Replace("Visita lo Store di ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Marca: ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static bool HasAnyData(ProductData product)
    {
        return !string.IsNullOrWhiteSpace(product.Title) ||
               !string.IsNullOrWhiteSpace(product.Brand) ||
               !string.IsNullOrWhiteSpace(product.Sku) ||
               !string.IsNullOrWhiteSpace(product.Description) ||
               !string.IsNullOrWhiteSpace(product.ImageUrl) ||
               product.Images.Count > 0;
    }
}
