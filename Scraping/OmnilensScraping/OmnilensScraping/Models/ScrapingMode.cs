using System.Text.Json.Serialization;

namespace OmnilensScraping.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScrapingMode
{
    Auto,
    Html,
    JsonLd,
    EmbeddedState,
    Browser
}
