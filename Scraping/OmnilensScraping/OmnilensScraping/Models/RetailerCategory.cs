using System.Text.Json.Serialization;

namespace OmnilensScraping.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetailerCategory
{
    Electronics,
    Marketplace,
    Pharmacy
}
