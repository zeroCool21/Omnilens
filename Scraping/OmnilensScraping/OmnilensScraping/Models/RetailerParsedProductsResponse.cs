namespace OmnilensScraping.Models;

/// <summary>
/// Risposta batch con i primi prodotti del catalogo gia parsati.
/// </summary>
public class RetailerParsedProductsResponse
{
    /// <summary>
    /// Retailer interrogato.
    /// </summary>
    public RetailerType Retailer { get; set; }

    /// <summary>
    /// Numero di prodotti richiesti dal chiamante.
    /// </summary>
    public int RequestedTake { get; set; }

    /// <summary>
    /// Numero di risultati effettivamente restituiti.
    /// </summary>
    public int Returned { get; set; }

    /// <summary>
    /// Numero di prodotti parsati con successo.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Numero di prodotti falliti durante il batch.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Modalita richiesta dal chiamante.
    /// </summary>
    public ScrapingMode RequestedMode { get; set; }

    /// <summary>
    /// Parallelismo effettivamente usato per il batch.
    /// </summary>
    public int AppliedMaxConcurrency { get; set; }

    /// <summary>
    /// Durata totale dell'operazione in millisecondi.
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Durata totale dell'operazione in secondi.
    /// </summary>
    public double ElapsedSeconds { get; set; }

    /// <summary>
    /// Durata totale in formato leggibile: secondi, minuti o ore.
    /// </summary>
    public string ElapsedDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Prodotti parsati e relativo esito.
    /// </summary>
    public IReadOnlyCollection<ScrapeProductResponse> Products { get; set; } = Array.Empty<ScrapeProductResponse>();
}
