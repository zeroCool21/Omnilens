using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class ScraperExecutionResult
{
    public ProductData Product { get; init; } = new();
    public ScrapingMode AppliedMode { get; init; }
    public List<string> AppliedStrategies { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
