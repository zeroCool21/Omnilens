namespace OmnilensScraping.Models;

/// <summary>
/// Dati prodotto normalizzati estratti da una pagina e-commerce.
/// </summary>
public class ProductData
{
    /// <summary>
    /// URL della pagina prodotto.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Nome del retailer di provenienza.
    /// </summary>
    public string Retailer { get; set; } = string.Empty;

    /// <summary>
    /// Tecnica di estrazione usata per ottenere il risultato.
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Titolo del prodotto.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Brand del prodotto.
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// SKU o codice articolo trovato in pagina.
    /// </summary>
    public string? Sku { get; set; }

    /// <summary>
    /// GTIN o EAN, se disponibile.
    /// </summary>
    public string? Gtin { get; set; }

    /// <summary>
    /// Prezzo numerico normalizzato.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Prezzo come testo grezzo estratto dalla pagina.
    /// </summary>
    public string? PriceText { get; set; }

    /// <summary>
    /// Valuta associata al prezzo.
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Disponibilita dichiarata dal retailer.
    /// </summary>
    public string? Availability { get; set; }

    /// <summary>
    /// Descrizione prodotto, se presente.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// URL dell'immagine principale.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Elenco immagini raccolte dalla pagina.
    /// </summary>
    public List<string> Images { get; set; } = new();

    /// <summary>
    /// Specifiche tecniche in forma chiave-valore.
    /// </summary>
    public Dictionary<string, string> Specifications { get; set; } = new();

    /// <summary>
    /// Timestamp UTC in cui il prodotto e stato estratto.
    /// </summary>
    public DateTimeOffset ScrapedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
