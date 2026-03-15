namespace OmnilensScraping.Models;

/// <summary>
/// Descrive il livello di affidabilita della sorgente catalogo usata dal backend.
/// </summary>
public class CatalogCoverageStatus
{
    /// <summary>
    /// Tipo di sorgente usata: PublicSitemap, PublicCrawlSnapshot o AuthoritativeSnapshot.
    /// </summary>
    public string SourceKind { get; set; } = string.Empty;

    /// <summary>
    /// True solo quando il backend sta leggendo una sorgente dichiarata completa.
    /// </summary>
    public bool IsGuaranteedComplete { get; set; }

    /// <summary>
    /// Nota operativa sulla copertura reale del catalogo.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}
