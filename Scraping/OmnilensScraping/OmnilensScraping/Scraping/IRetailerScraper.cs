using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public interface IRetailerScraper
{
    RetailerType Retailer { get; }
    bool CanHandle(Uri url);
    Task<ScraperExecutionResult> ScrapeAsync(Uri url, ScrapingMode mode, CancellationToken cancellationToken);
}
