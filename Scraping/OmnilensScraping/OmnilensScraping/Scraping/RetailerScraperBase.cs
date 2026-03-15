using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public abstract class RetailerScraperBase : IRetailerScraper
{
    protected RetailerScraperBase(PageContentService pageContentService)
    {
        PageContentService = pageContentService;
    }

    protected PageContentService PageContentService { get; }

    public abstract RetailerType Retailer { get; }

    public abstract bool CanHandle(Uri url);

    public abstract Task<ScraperExecutionResult> ScrapeAsync(Uri url, ScrapingMode mode, CancellationToken cancellationToken);

    protected ProductData CreateProduct(Uri url, ScrapingMode mode)
    {
        return new ProductData
        {
            Url = url.ToString(),
            Retailer = Retailer.ToString(),
            Method = mode.ToString()
        };
    }

    protected static void ApplyFrom(ProductData source, ProductData target)
    {
        target.Title ??= source.Title;
        target.Brand ??= source.Brand;
        target.Sku ??= source.Sku;
        target.Gtin ??= source.Gtin;
        target.Price ??= source.Price;
        target.PriceText ??= source.PriceText;
        target.Currency ??= source.Currency;
        target.Availability ??= source.Availability;
        target.Description ??= source.Description;
        target.ImageUrl ??= source.ImageUrl;

        foreach (var image in source.Images.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!target.Images.Contains(image, StringComparer.OrdinalIgnoreCase))
            {
                target.Images.Add(image);
            }
        }

        foreach (var specification in source.Specifications)
        {
            target.Specifications[specification.Key] = specification.Value;
        }
    }
}
