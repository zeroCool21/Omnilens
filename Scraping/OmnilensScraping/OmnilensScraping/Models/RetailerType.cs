using System.Text.Json.Serialization;

namespace OmnilensScraping.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetailerType
{
    Unieuro,
    MediaWorld,
    Euronics,
    AmazonIt,
    Redcare,
    DrMax,
    Farmasave,
    FarmaciaLoreto,
    EFarma,
    Farmacie1000,
    TopFarmacia,
    TuttoFarma,
    FarmaIt,
    BenuFarma
}
