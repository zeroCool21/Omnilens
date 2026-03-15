using System.Text.Json.Nodes;
using HtmlAgilityPack;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class EuronicsRetailerScraper : RetailerScraperBase
{
    public EuronicsRetailerScraper(PageContentService pageContentService)
        : base(pageContentService)
    {
    }

    public override RetailerType Retailer => RetailerType.Euronics;

    public override bool CanHandle(Uri url)
    {
        return url.Host.Contains("euronics.it", StringComparison.OrdinalIgnoreCase);
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

        if (mode is ScrapingMode.JsonLd or ScrapingMode.Auto or ScrapingMode.Browser)
        {
            var jsonLdProduct = StructuredDataParser.TryParseJsonLd(html, url);
            if (jsonLdProduct is not null)
            {
                ApplyFrom(jsonLdProduct, product);
                strategies.Add("json-ld");
                if (mode == ScrapingMode.Auto)
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

        if (mode is ScrapingMode.Html or ScrapingMode.Auto or ScrapingMode.Browser)
        {
            var htmlProduct = ParseHtml(document, url);
            ApplyFrom(htmlProduct, product);
            strategies.Add("html");
        }

        if (string.IsNullOrWhiteSpace(product.Title) || !product.Price.HasValue)
        {
            warnings.Add("Estrazione parziale su Euronics: la scheda prodotto resta comunque leggibile da HTML e JSON-LD.");
        }

        return new ScraperExecutionResult
        {
            Product = product,
            AppliedMode = appliedMode,
            AppliedStrategies = strategies,
            Warnings = warnings
        };
    }

    private static ProductData ParseHtml(HtmlDocument document, Uri url)
    {
        var product = new ProductData { Url = url.ToString() };
        product.Title = HtmlHelpers.NormalizeText(document.DocumentNode.SelectSingleNode("//title")?.InnerText);
        product.Description = string.Join(
            " ",
            (document.DocumentNode.SelectNodes("//*[contains(@class,'description-wrapper')]//p") ?? Enumerable.Empty<HtmlNode>())
            .Select(node => HtmlHelpers.NormalizeText(node.InnerText))
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        product.PriceText = HtmlHelpers.GetTextByXPath(document, "(//*[contains(@class,'product-price')])[1]");
        product.Price = HtmlHelpers.ParsePrice(product.PriceText);

        foreach (var item in document.DocumentNode.SelectNodes("//*[contains(@class,'techSpeItem')]") ?? Enumerable.Empty<HtmlNode>())
        {
            var section = HtmlHelpers.NormalizeText(item.SelectSingleNode(".//*[contains(@class,'techSpeItemTitle')]")?.InnerText);
            foreach (var row in item.SelectNodes(".//*[contains(@class,'techSpeRow')]") ?? Enumerable.Empty<HtmlNode>())
            {
                var key = row.SelectSingleNode(".//*[contains(@class,'keyMapLeft')]")?.InnerText;
                var value = row.SelectSingleNode(".//*[contains(@class,'keyMapRight')]")?.InnerText;
                HtmlHelpers.AddSpecification(product.Specifications, key, value, section);
            }
        }

        return product;
    }

    private static ProductData? ParseEmbeddedState(HtmlDocument document, Uri url)
    {
        var node = document.DocumentNode.SelectSingleNode("//*[@data-productinfo]");
        var rawProductInfo = node?.GetAttributeValue("data-productinfo", null);
        if (string.IsNullOrWhiteSpace(rawProductInfo))
        {
            return null;
        }

        try
        {
            var jsonNode = JsonNode.Parse(rawProductInfo);
            var product = new ProductData
            {
                Url = url.ToString(),
                Gtin = StructuredDataParser.FindFirstStringByPropertyName(jsonNode, "ean"),
                Title = StructuredDataParser.FindFirstStringByPropertyName(jsonNode, "name"),
                Sku = StructuredDataParser.FindFirstStringByPropertyName(jsonNode, "id"),
                Brand = StructuredDataParser.FindFirstStringByPropertyName(jsonNode, "brand"),
                PriceText = StructuredDataParser.FindFirstStringByPropertyName(jsonNode, "price")
            };

            product.Price = HtmlHelpers.ParsePrice(product.PriceText);
            return product;
        }
        catch
        {
            return null;
        }
    }
}
