using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class UnieuroRetailerScraper : RetailerScraperBase
{
    private static readonly Regex SwogoTagRegex = new("<p id=\"swogotag\"[^>]*>(?<value>.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public UnieuroRetailerScraper(PageContentService pageContentService)
        : base(pageContentService)
    {
    }

    public override RetailerType Retailer => RetailerType.Unieuro;

    public override bool CanHandle(Uri url)
    {
        return url.Host.Contains("unieuro.it", StringComparison.OrdinalIgnoreCase);
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

        PopulateFromMeta(document, url, product);

        if (mode is ScrapingMode.EmbeddedState or ScrapingMode.Auto or ScrapingMode.Browser)
        {
            var embedded = ParseEmbeddedState(html, document, url);
            if (embedded is not null)
            {
                ApplyFrom(embedded, product);
                strategies.Add("embedded-state");
                if (mode == ScrapingMode.Auto)
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

        if (mode == ScrapingMode.JsonLd)
        {
            var jsonLdProduct = StructuredDataParser.TryParseJsonLd(html, url);
            if (jsonLdProduct is null)
            {
                warnings.Add("La pagina Unieuro corrente non espone dati JSON-LD utili per il prodotto richiesto.");
            }
            else
            {
                ApplyFrom(jsonLdProduct, product);
                strategies.Add("json-ld");
            }
        }

        if (string.IsNullOrWhiteSpace(product.Title) || !product.Price.HasValue)
        {
            warnings.Add("Estrazione parziale: per Unieuro i dati piu affidabili arrivano dallo stato SSR incorporato.");
        }

        return new ScraperExecutionResult
        {
            Product = product,
            AppliedMode = appliedMode,
            AppliedStrategies = strategies,
            Warnings = warnings
        };
    }

    private static void PopulateFromMeta(HtmlDocument document, Uri url, ProductData product)
    {
        product.Title ??= HtmlHelpers.NormalizeText(document.DocumentNode.SelectSingleNode("//title")?.InnerText);
        product.Description ??= HtmlHelpers.GetMetaContent(document, "description") ??
                               HtmlHelpers.GetMetaContent(document, "og:description");

        var imageUrl = HtmlHelpers.GetMetaContent(document, "og:image");
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var absoluteImageUrl = HtmlHelpers.MakeAbsolute(url, imageUrl);
            product.ImageUrl ??= absoluteImageUrl;
            if (!product.Images.Contains(absoluteImageUrl, StringComparer.OrdinalIgnoreCase))
            {
                product.Images.Add(absoluteImageUrl);
            }
        }
    }

    private static ProductData ParseHtml(HtmlDocument document, Uri url)
    {
        var product = new ProductData { Url = url.ToString() };
        product.Title = HtmlHelpers.NormalizeText(document.DocumentNode.SelectSingleNode("//title")?.InnerText);
        product.Description = HtmlHelpers.GetMetaContent(document, "description");
        return product;
    }

    private static ProductData? ParseEmbeddedState(string html, HtmlDocument document, Uri url)
    {
        var product = new ProductData { Url = url.ToString() };
        var swogoMatch = SwogoTagRegex.Match(html);
        if (swogoMatch.Success)
        {
            var segments = HtmlHelpers.NormalizeText(swogoMatch.Groups["value"].Value)?.Split('|', StringSplitOptions.TrimEntries);
            if (segments?.Length >= 4)
            {
                product.Title = HtmlHelpers.NormalizeText(segments[0]);
                product.Sku = HtmlHelpers.NormalizeText(segments[1]);
                product.Availability = HtmlHelpers.NormalizeAvailability(segments[2]);
                product.Price = HtmlHelpers.ParsePrice(segments[3]);
                product.PriceText = HtmlHelpers.NormalizeText(segments[3]);
            }
        }

        var stateJson = HtmlHelpers.TryReadScriptById(document, "ng-state");
        if (!string.IsNullOrWhiteSpace(stateJson))
        {
            try
            {
                var stateNode = JsonNode.Parse(stateJson);
                var stateProduct = StructuredDataParser.FindFirstObjectContaining(stateNode, "code", "priceValue") ??
                                   StructuredDataParser.FindFirstObjectContaining(stateNode, "code", "summary");

                if (stateProduct is not null)
                {
                    product.Sku ??= StructuredDataParser.FindFirstStringByPropertyName(stateProduct, "code", "sku");
                    product.Brand ??= StructuredDataParser.FindFirstStringByPropertyName(stateProduct, "brandName", "brand");
                    product.Gtin ??= StructuredDataParser.FindFirstStringByPropertyName(stateProduct, "ean");
                    product.Description ??= StructuredDataParser.FindFirstStringByPropertyName(stateProduct, "summary", "description");

                    var formattedPrice = StructuredDataParser.FindFirstStringByPropertyName(stateProduct, "formattedValue");
                    product.PriceText ??= formattedPrice;
                    product.Price ??= HtmlHelpers.ParsePrice(formattedPrice);

                    var imageCandidates = StructuredDataParser.FindAllStringByPropertyName(stateProduct, "url")
                        .Where(value => value.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                        value.Contains(".png", StringComparison.OrdinalIgnoreCase))
                        .Select(value => HtmlHelpers.MakeAbsolute(url, value))
                        .ToArray();

                    if (imageCandidates.Length > 0)
                    {
                        product.ImageUrl ??= imageCandidates.First();
                        foreach (var image in imageCandidates)
                        {
                            if (!product.Images.Contains(image, StringComparer.OrdinalIgnoreCase))
                            {
                                product.Images.Add(image);
                            }
                        }
                    }
                }
            }
            catch
            {
                return string.IsNullOrWhiteSpace(product.Title) && !product.Price.HasValue ? null : product;
            }
        }

        return string.IsNullOrWhiteSpace(product.Title) && !product.Price.HasValue && string.IsNullOrWhiteSpace(product.Sku)
            ? null
            : product;
    }
}
