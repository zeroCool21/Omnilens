namespace OmnilensScraping.Models;

/// <summary>
/// Risultato di un singolo probe di concorrenza.
/// </summary>
public class ConcurrencyProbeResult
{
    /// <summary>
    /// Numero di richieste eseguite in parallelo durante il probe.
    /// </summary>
    public int Concurrency { get; set; }

    /// <summary>
    /// Numero totale di richieste eseguite nel probe.
    /// </summary>
    public int Requests { get; set; }

    /// <summary>
    /// Numero di richieste concluse con successo.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Numero di richieste fallite.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Numero di richieste che hanno mostrato segnali compatibili con blocchi o throttle.
    /// </summary>
    public int BanSignalCount { get; set; }

    /// <summary>
    /// Percentuale di successo del probe, tra 0 e 1.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Latenza media del probe in millisecondi.
    /// </summary>
    public long AverageDurationMs { get; set; }

    /// <summary>
    /// Percentile 95 della latenza del probe in millisecondi.
    /// </summary>
    public long P95DurationMs { get; set; }
}
