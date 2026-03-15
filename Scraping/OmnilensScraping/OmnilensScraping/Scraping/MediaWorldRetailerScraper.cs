using HtmlAgilityPack;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class MediaWorldRetailerScraper : RetailerScraperBase
{
    public MediaWorldRetailerScraper(PageContentService pageContentService)
        : base(pageContentService)
    {
    }

    public override RetailerType Retailer => RetailerType.MediaWorld;

    public override bool CanHandle(Uri url)
    {
        return url.Host.Contains("mediaworld.it", StringComparison.OrdinalIgnoreCase);
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

        if (mode is ScrapingMode.Html or ScrapingMode.Auto or ScrapingMode.Browser or ScrapingMode.EmbeddedState)
        {
            var htmlProduct = ParseHtml(document, url);
            ApplyFrom(htmlProduct, product);
            strategies.Add("html");
        }

        if (!product.Price.HasValue)
        {
            warnings.Add("MediaWorld puo rispondere con challenge anti-bot: se i dati sono vuoti prova la modalita Browser.");
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
        product.Description = HtmlHelpers.GetTextByXPath(document, "//*[@data-test='pdp-description-pim-fallback']");

        var availabilityText = HtmlHelpers.GetTextByXPath(document, "//*[@data-test='mms-pdp-details-availability']//p[1]");
        if (!string.IsNullOrWhiteSpace(availabilityText))
        {
            product.Availability = availabilityText;
        }

        foreach (var button in document.DocumentNode.SelectNodes("//*[@data-test='mms-pdp-details-mainfeatures']//button") ?? Enumerable.Empty<HtmlNode>())
        {
            var spans = button.SelectNodes(".//span");
            if (spans is { Count: >= 2 })
            {
                HtmlHelpers.AddSpecification(product.Specifications, spans[0].InnerText, spans[1].InnerText);
            }
        }

        foreach (var row in document.DocumentNode.SelectNodes("//*[@data-test='pdp-features-content']//tr[td]") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = row.SelectNodes("./td");
            if (cells is { Count: >= 2 })
            {
                HtmlHelpers.AddSpecification(product.Specifications, cells[0].InnerText, cells[1].InnerText, "Caratteristiche tecniche");
            }
        }

        return product;
    }
}
