using System.Text.Json.Serialization;

namespace OmnilensScraping.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetailerType
{
    Unieuro,
    MediaWorld,
    Euronics,
    AmazonIt
}
