using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

namespace OmnilensScraping.Scraping;

public class AmazonCatalogUrlSource : ICatalogUrlSource
{
    private static readonly Regex AmazonAsinRegex = new("^[A-Z0-9]{10}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmazonProductUrlRegex = new("(?:/dp/|/gp/product/)(?<asin>[A-Z0-9]{10})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AmazonCatalogOptions _options;
    private readonly AmazonCatalogBootstrapService _bootstrapService;

    public AmazonCatalogUrlSource(
        IOptions<AmazonCatalogOptions> options,
        AmazonCatalogBootstrapService bootstrapService)
    {
        _options = options.Value;
        _bootstrapService = bootstrapService;
    }

    public bool CanHandle(RetailerDefinition definition)
    {
        return definition.Retailer == Models.RetailerType.AmazonIt;
    }

    public async Task<IReadOnlyCollection<string>> GetSampleProductUrlsAsync(
        RetailerDefinition definition,
        int take,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return Array.Empty<string>();
        }

        return (await LoadCatalogUrlsAsync(definition, cancellationToken))
            .Take(take)
            .ToArray();
    }

    public async Task<int> CountProductUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        return (await LoadCatalogUrlsAsync(definition, cancellationToken)).Count;
    }

    public async Task<int> CountProductSourcesAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var files = ResolveCatalogFiles();
        if (files.Count == 0 && _options.AutoBootstrapIfMissing)
        {
            await _bootstrapService.BootstrapAsync(force: false, cancellationToken);
            files = ResolveCatalogFiles();
        }

        return files.Count;
    }

    private async Task<IReadOnlyCollection<string>> LoadCatalogUrlsAsync(
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        var files = ResolveCatalogFiles();
        if (files.Count == 0 && _options.AutoBootstrapIfMissing)
        {
            await _bootstrapService.BootstrapAsync(force: false, cancellationToken);
            files = ResolveCatalogFiles();
        }

        if (files.Count == 0)
        {
            throw new NotSupportedException(
                definition.CatalogNotes ??
                "Amazon IT non ha ancora una snapshot catalogo locale valida.");
        }

        var collectedUrls = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, _options.CatalogFileReadConcurrency),
                CancellationToken = cancellationToken
            },
            async (filePath, ct) =>
            {
                var fileUrls = new List<string>();
                await CollectUrlsFromFileAsync(filePath, fileUrls, ct);
                foreach (var url in fileUrls)
                {
                    collectedUrls.Add(url);
                }
            });

        var urls = collectedUrls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (urls.Length == 0)
        {
            throw new NotSupportedException(
                "I file catalogo Amazon configurati non contengono ASIN o URL prodotto Amazon validi.");
        }

        return urls;
    }

    private IReadOnlyCollection<string> ResolveCatalogFiles()
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in _options.FilePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var fullPath = Path.GetFullPath(filePath);
            if (File.Exists(fullPath))
            {
                results.Add(fullPath);
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.CatalogDirectory))
        {
            var directory = Path.GetFullPath(_options.CatalogDirectory);
            if (Directory.Exists(directory))
            {
                foreach (var pattern in _options.FilePatterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern)))
                {
                    foreach (var filePath in Directory.EnumerateFiles(directory, pattern, System.IO.SearchOption.AllDirectories))
                    {
                        results.Add(filePath);
                    }
                }
            }
        }

        return results
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task CollectUrlsFromFileAsync(
        string filePath,
        ICollection<string> urls,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);
        switch (extension.ToLowerInvariant())
        {
            case ".xml":
                await CollectUrlsFromXmlFileAsync(filePath, urls, cancellationToken);
                break;
            case ".json":
                await CollectUrlsFromJsonFileAsync(filePath, urls, cancellationToken);
                break;
            case ".jsonl":
                await CollectUrlsFromJsonLinesFileAsync(filePath, urls, cancellationToken);
                break;
            case ".csv":
            case ".tsv":
                CollectUrlsFromDelimitedFile(filePath, urls);
                break;
            default:
                await CollectUrlsFromTextFileAsync(filePath, urls, cancellationToken);
                break;
        }
    }

    private async Task CollectUrlsFromXmlFileAsync(
        string filePath,
        ICollection<string> urls,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        foreach (var location in content.Split("<loc>", StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            AddCatalogCandidate(location.Split("</loc>", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), urls);
        }
    }

    private async Task CollectUrlsFromTextFileAsync(
        string filePath,
        ICollection<string> urls,
        CancellationToken cancellationToken)
    {
        foreach (var line in await File.ReadAllLinesAsync(filePath, cancellationToken))
        {
            AddCatalogCandidate(line, urls);
        }
    }

    private async Task CollectUrlsFromJsonFileAsync(
        string filePath,
        ICollection<string> urls,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        try
        {
            CollectCatalogCandidates(JsonNode.Parse(content), urls);
        }
        catch
        {
            // Ignora file JSON non validi.
        }
    }

    private async Task CollectUrlsFromJsonLinesFileAsync(
        string filePath,
        ICollection<string> urls,
        CancellationToken cancellationToken)
    {
        foreach (var line in await File.ReadAllLinesAsync(filePath, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                CollectCatalogCandidates(JsonNode.Parse(line), urls);
            }
            catch
            {
                AddCatalogCandidate(line, urls);
            }
        }
    }

    private void CollectUrlsFromDelimitedFile(string filePath, ICollection<string> urls)
    {
        using var parser = new TextFieldParser(filePath);
        parser.TextFieldType = FieldType.Delimited;
        parser.HasFieldsEnclosedInQuotes = true;
        parser.SetDelimiters(",", "\t", ";");

        if (parser.EndOfData)
        {
            return;
        }

        var firstRow = parser.ReadFields() ?? Array.Empty<string>();
        var headerIndexes = GetKnownHeaderIndexes(firstRow);
        var hasHeader = headerIndexes.Count > 0;

        if (!hasHeader)
        {
            AddCatalogFields(firstRow, urls);
        }

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? Array.Empty<string>();
            if (hasHeader)
            {
                foreach (var index in headerIndexes)
                {
                    if (index >= 0 && index < fields.Length)
                    {
                        AddCatalogCandidate(fields[index], urls);
                    }
                }
            }
            else
            {
                AddCatalogFields(fields, urls);
            }
        }
    }

    private static IReadOnlyCollection<int> GetKnownHeaderIndexes(IReadOnlyList<string> headerRow)
    {
        var knownHeaders = new[] { "detailpageurl", "detailurl", "producturl", "url", "link", "asin" };

        return headerRow
            .Select((value, index) => new
            {
                Index = index,
                Value = HtmlHelpers.NormalizeText(value)?.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value) &&
                            knownHeaders.Contains(entry.Value, StringComparer.OrdinalIgnoreCase))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static void AddCatalogFields(IEnumerable<string> fields, ICollection<string> urls)
    {
        foreach (var field in fields)
        {
            AddCatalogCandidate(field, urls);
        }
    }

    private static void CollectCatalogCandidates(JsonNode? node, ICollection<string> urls)
    {
        if (node is null)
        {
            return;
        }

        switch (node)
        {
            case JsonValue value:
                AddCatalogCandidate(value.ToString(), urls);
                return;
            case JsonArray array:
                foreach (var item in array)
                {
                    CollectCatalogCandidates(item, urls);
                }
                return;
            case JsonObject jsonObject:
                foreach (var property in jsonObject)
                {
                    AddCatalogCandidate(property.Value?.ToString(), urls);
                    CollectCatalogCandidates(property.Value, urls);
                }
                return;
        }
    }

    private static void AddCatalogCandidate(string? candidate, ICollection<string> urls)
    {
        var normalized = NormalizeCatalogValue(candidate);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            urls.Add(normalized);
        }
    }

    private static string? NormalizeCatalogValue(string? candidate)
    {
        var value = HtmlHelpers.NormalizeText(candidate);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var asinMatch = AmazonProductUrlRegex.Match(value);
        if (asinMatch.Success)
        {
            return BuildAmazonProductUrl(asinMatch.Groups["asin"].Value);
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUrl) &&
            absoluteUrl.Host.Contains("amazon.it", StringComparison.OrdinalIgnoreCase))
        {
            var canonicalMatch = AmazonProductUrlRegex.Match(absoluteUrl.AbsoluteUri);
            return canonicalMatch.Success
                ? BuildAmazonProductUrl(canonicalMatch.Groups["asin"].Value)
                : absoluteUrl.ToString();
        }

        return AmazonAsinRegex.IsMatch(value) ? BuildAmazonProductUrl(value) : null;
    }

    private static string BuildAmazonProductUrl(string asin)
    {
        return $"https://www.amazon.it/dp/{asin.ToUpperInvariant()}";
    }
}
