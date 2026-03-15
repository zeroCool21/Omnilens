using System.Text.Json.Nodes;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

internal static class StructuredDataParser
{
    public static ProductData? TryParseJsonLd(string html, Uri url)
    {
        foreach (var block in HtmlHelpers.ExtractJsonLdBlocks(html))
        {
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(block);
            }
            catch
            {
                continue;
            }

            foreach (var productNode in EnumerateProductNodes(root))
            {
                var product = MapProductNode(productNode, url);
                if (!string.IsNullOrWhiteSpace(product.Title) || product.Price.HasValue)
                {
                    return product;
                }
            }
        }

        return null;
    }

    public static JsonObject? FindFirstObjectContaining(JsonNode? node, params string[] keys)
    {
        if (node is JsonObject jsonObject)
        {
            if (keys.All(key => jsonObject.ContainsKey(key)))
            {
                return jsonObject;
            }

            foreach (var property in jsonObject)
            {
                var candidate = FindFirstObjectContaining(property.Value, keys);
                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }

        if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                var candidate = FindFirstObjectContaining(item, keys);
                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static string? FindFirstStringByPropertyName(JsonNode? node, params string[] names)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var name in names)
            {
                if (jsonObject.TryGetPropertyValue(name, out var value))
                {
                    var stringValue = HtmlHelpers.NormalizeText(value?.ToString());
                    if (!string.IsNullOrWhiteSpace(stringValue))
                    {
                        return stringValue;
                    }
                }
            }

            foreach (var property in jsonObject)
            {
                var candidate = FindFirstStringByPropertyName(property.Value, names);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
        }

        if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                var candidate = FindFirstStringByPropertyName(item, names);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static IReadOnlyCollection<string> FindAllStringByPropertyName(JsonNode? node, params string[] names)
    {
        var results = new List<string>();
        CollectStrings(node, results, names);
        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void CollectStrings(JsonNode? node, ICollection<string> results, params string[] names)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var name in names)
            {
                if (jsonObject.TryGetPropertyValue(name, out var value))
                {
                    var stringValue = HtmlHelpers.NormalizeText(value?.ToString());
                    if (!string.IsNullOrWhiteSpace(stringValue))
                    {
                        results.Add(stringValue);
                    }
                }
            }

            foreach (var property in jsonObject)
            {
                CollectStrings(property.Value, results, names);
            }
        }

        if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                CollectStrings(item, results, names);
            }
        }
    }

    private static IEnumerable<JsonObject> EnumerateProductNodes(JsonNode? node)
    {
        if (node is null)
        {
            yield break;
        }

        if (node is JsonObject jsonObject)
        {
            var type = HtmlHelpers.NormalizeText(jsonObject["@type"]?.ToString());
            if (!string.IsNullOrWhiteSpace(type) &&
                type.Contains("Product", StringComparison.OrdinalIgnoreCase))
            {
                yield return jsonObject;
            }

            if (!string.IsNullOrWhiteSpace(type) &&
                type.Contains("BuyAction", StringComparison.OrdinalIgnoreCase) &&
                jsonObject["object"] is JsonObject productObject)
            {
                yield return productObject;
            }

            foreach (var property in jsonObject)
            {
                foreach (var child in EnumerateProductNodes(property.Value))
                {
                    yield return child;
                }
            }
        }

        if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                foreach (var child in EnumerateProductNodes(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static ProductData MapProductNode(JsonObject productNode, Uri url)
    {
        var brand = productNode["brand"] switch
        {
            JsonObject brandNode => HtmlHelpers.NormalizeText(brandNode["name"]?.ToString()),
            JsonValue brandValue => HtmlHelpers.NormalizeText(brandValue.ToString()),
            _ => null
        };

        var offerNode = productNode["offers"] switch
        {
            JsonArray offerArray => offerArray.OfType<JsonObject>().FirstOrDefault(),
            JsonObject offerObject => offerObject,
            _ => null
        };

        var images = productNode["image"] switch
        {
            JsonArray imageArray => imageArray.Select(node => HtmlHelpers.MakeAbsolute(url, node?.ToString()))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray(),
            JsonValue imageValue => new[] { HtmlHelpers.MakeAbsolute(url, imageValue.ToString()) },
            _ => Array.Empty<string>()
        };

        return new ProductData
        {
            Url = url.ToString(),
            Title = HtmlHelpers.NormalizeText(productNode["name"]?.ToString()),
            Description = HtmlHelpers.NormalizeText(productNode["description"]?.ToString()),
            Brand = brand,
            Sku = HtmlHelpers.NormalizeText(productNode["sku"]?.ToString()) ??
                  HtmlHelpers.NormalizeText(productNode["mpn"]?.ToString()),
            Gtin = HtmlHelpers.NormalizeText(productNode["gtin13"]?.ToString()) ??
                   HtmlHelpers.NormalizeText(productNode["gtin"]?.ToString()),
            Price = HtmlHelpers.ParsePrice(offerNode?["price"]?.ToString()),
            PriceText = HtmlHelpers.NormalizeText(offerNode?["price"]?.ToString()),
            Currency = HtmlHelpers.NormalizeText(offerNode?["priceCurrency"]?.ToString()),
            Availability = HtmlHelpers.NormalizeAvailability(offerNode?["availability"]?.ToString()),
            ImageUrl = images.FirstOrDefault(),
            Images = images.ToList()
        };
    }
}
