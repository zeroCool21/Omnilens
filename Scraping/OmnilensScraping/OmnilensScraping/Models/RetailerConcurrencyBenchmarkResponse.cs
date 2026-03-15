namespace OmnilensScraping.Models;

/// <summary>
/// Risposta del benchmark di concorrenza per un retailer.
/// </summary>
public class RetailerConcurrencyBenchmarkResponse
{
    /// <summary>
    /// Retailer testato.
    /// </summary>
    public RetailerType Retailer { get; set; }

    /// <summary>
    /// Modalita usata durante i test.
    /// </summary>
    public ScrapingMode RequestedMode { get; set; }

    /// <summary>
    /// Numero di URL campione usati per i probe.
    /// </summary>
    public int SampleSize { get; set; }

    /// <summary>
    /// Primo livello di concorrenza testato.
    /// </summary>
    public int StartConcurrency { get; set; }

    /// <summary>
    /// Livello massimo di concorrenza testato.
    /// </summary>
    public int MaxConcurrency { get; set; }

    /// <summary>
    /// Soglia minima di success rate accettata per considerare stabile un probe.
    /// </summary>
    public double SuccessRateThreshold { get; set; }

    /// <summary>
    /// Valore massimo raccomandato sulla base dei test eseguiti.
    /// </summary>
    public int RecommendedMaxConcurrency { get; set; }

    /// <summary>
    /// Modalita consigliata per ripetere il test o il batch.
    /// </summary>
    public ScrapingMode RecommendedMode { get; set; }

    /// <summary>
    /// Motivo per cui il benchmark si e fermato.
    /// </summary>
    public string StopReason { get; set; } = string.Empty;

    /// <summary>
    /// Nota metodologica sulla natura osservazionale del benchmark.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Elenco dei probe eseguiti con metriche per ogni livello di concorrenza.
    /// </summary>
    public IReadOnlyCollection<ConcurrencyProbeResult> Probes { get; set; } = Array.Empty<ConcurrencyProbeResult>();
}
