namespace OmnilensScraping.Models;

/// <summary>
/// Esito dello scraping di un singolo prodotto.
/// </summary>
public class ScrapeProductResponse
{
    /// <summary>
    /// Indica se lo scraping del prodotto e andato a buon fine.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Messaggio di errore o nota operativa restituita dal backend.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Retailer effettivamente associato alla pagina analizzata.
    /// </summary>
    public RetailerType Retailer { get; set; }

    /// <summary>
    /// Modalita richiesta dal chiamante.
    /// </summary>
    public ScrapingMode RequestedMode { get; set; }

    /// <summary>
    /// Modalita realmente applicata dal motore di scraping.
    /// </summary>
    public ScrapingMode AppliedMode { get; set; }

    /// <summary>
    /// Durata totale dello scraping del singolo prodotto in millisecondi.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Strategie interne applicate durante il parsing.
    /// </summary>
    public List<string> AppliedStrategies { get; set; } = new();

    /// <summary>
    /// Avvisi non bloccanti emersi durante lo scraping.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Dati prodotto estratti dalla pagina, se presenti.
    /// </summary>
    public ProductData? Product { get; set; }
}
