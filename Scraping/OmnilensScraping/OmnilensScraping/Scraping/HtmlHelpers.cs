using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace OmnilensScraping.Scraping;

internal static class HtmlHelpers
{
    private static readonly Regex JsonScriptRegex = new(
        "<script[^>]*type=[\"']application/ld\\+json[\"'][^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static HtmlDocument Load(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }

    public static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(value);
        var normalized = Regex.Replace(decoded, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static string? GetMetaContent(HtmlDocument document, string key)
    {
        var node = document.DocumentNode.SelectSingleNode($"//meta[@name='{key}']") ??
                   document.DocumentNode.SelectSingleNode($"//meta[@property='{key}']");

        return NormalizeText(node?.GetAttributeValue("content", null));
    }

    public static string? GetTextByXPath(HtmlDocument document, string xpath)
    {
        return NormalizeText(document.DocumentNode.SelectSingleNode(xpath)?.InnerText);
    }

    public static string? TryReadScriptById(HtmlDocument document, string id)
    {
        return NormalizeText(document.DocumentNode.SelectSingleNode($"//script[@id='{id}']")?.InnerText);
    }

    public static IReadOnlyCollection<string> ExtractJsonLdBlocks(string html)
    {
        return JsonScriptRegex.Matches(html)
            .Select(match => NormalizeText(match.Groups["json"].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    public static decimal? ParsePrice(string? input)
    {
        var normalized = NormalizeText(input);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        normalized = normalized
            .Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("€", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\u00a0", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (normalized.Contains(','))
        {
            normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal);
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var price)
            ? price
            : null;
    }

    public static string? NormalizeAvailability(string? value)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Contains('/'))
        {
            normalized = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized
            .Replace("http://schema.org/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("https://schema.org/", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public static string MakeAbsolute(Uri pageUrl, string? candidate)
    {
        var normalized = NormalizeText(candidate);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return Uri.TryCreate(normalized, UriKind.Absolute, out var absolute)
            ? absolute.ToString()
            : new Uri(pageUrl, normalized).ToString();
    }

    public static void AddSpecification(IDictionary<string, string> specifications, string? key, string? value, string? section = null)
    {
        var normalizedKey = NormalizeText(key);
        var normalizedValue = NormalizeText(value);

        if (string.IsNullOrWhiteSpace(normalizedKey) || string.IsNullOrWhiteSpace(normalizedValue))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(section))
        {
            normalizedKey = $"{NormalizeText(section)} - {normalizedKey}";
        }

        specifications[normalizedKey] = normalizedValue;
    }
}
