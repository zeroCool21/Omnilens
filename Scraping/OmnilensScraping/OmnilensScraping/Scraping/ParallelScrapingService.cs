using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmnilensScraping.Models;

namespace OmnilensScraping.Scraping;

public class ParallelScrapingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScrapingOptions _options;

    public ParallelScrapingService(
        IServiceScopeFactory scopeFactory,
        IOptions<ScrapingOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    public int ResolveBatchConcurrency(int requestedMaxConcurrency)
    {
        return requestedMaxConcurrency > 0
            ? requestedMaxConcurrency
            : Math.Max(1, _options.MaxBatchConcurrency);
    }

    public async Task<IReadOnlyCollection<ScrapeProductResponse>> ScrapeManyAsync(
        RetailerType retailer,
        IReadOnlyCollection<string> urls,
        ScrapingMode mode,
        int requestedMaxConcurrency,
        CancellationToken cancellationToken)
    {
        var indexedUrls = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select((url, index) => new { Url = url, Index = index })
            .ToArray();

        if (indexedUrls.Length == 0)
        {
            return Array.Empty<ScrapeProductResponse>();
        }

        var responses = new ScrapeProductResponse[indexedUrls.Length];
        var maxConcurrency = ResolveBatchConcurrency(requestedMaxConcurrency);

        await Parallel.ForEachAsync(
            indexedUrls,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = cancellationToken
            },
            async (entry, ct) =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<ScrapingCoordinator>();

                responses[entry.Index] = await TryScrapeAsync(
                    coordinator,
                    new ScrapeProductRequest
                    {
                        Url = entry.Url,
                        Retailer = retailer,
                        Mode = mode
                    },
                    ct);
            });

        return responses;
    }

    private static async Task<ScrapeProductResponse> TryScrapeAsync(
        ScrapingCoordinator coordinator,
        ScrapeProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await coordinator.ScrapeAsync(request, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BuildFailedResponse(request, exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return BuildFailedResponse(request, $"Errore HTTP durante lo scraping: {exception.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BuildFailedResponse(request, $"Errore interno: {exception.Message}");
        }
    }

    private static ScrapeProductResponse BuildFailedResponse(
        ScrapeProductRequest request,
        string message)
    {
        return new ScrapeProductResponse
        {
            Success = false,
            Message = message,
            Retailer = request.Retailer ?? default,
            RequestedMode = request.Mode
        };
    }
}
