namespace OmnilensScraping.Models;

/// <summary>
/// Richiesta di scraping per un singolo prodotto.
/// </summary>
public class ScrapeProductRequest
{
    /// <summary>
    /// URL completo della pagina prodotto da analizzare.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Retailer opzionale. Se omesso, il backend prova a ricavarlo dall'host dell'URL.
    /// </summary>
    public RetailerType? Retailer { get; set; }

    /// <summary>
    /// Modalita di scraping da usare: Auto, Html, JsonLd, EmbeddedState o Browser.
    /// </summary>
    public ScrapingMode Mode { get; set; } = ScrapingMode.Auto;
}
