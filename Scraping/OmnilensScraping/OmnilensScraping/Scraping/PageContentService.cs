using Microsoft.Extensions.Options;

namespace OmnilensScraping.Scraping;

public class PageContentService
{
    public const string ClientName = "scraping-client";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PlaywrightBrowserService _browserService;
    private readonly ScrapingOptions _options;
    private readonly SemaphoreSlim _httpConcurrencyGate;

    public PageContentService(
        IHttpClientFactory httpClientFactory,
        PlaywrightBrowserService browserService,
        IOptions<ScrapingOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _browserService = browserService;
        _options = options.Value;
        _httpConcurrencyGate = new SemaphoreSlim(
            Math.Max(1, _options.MaxHttpConcurrency),
            Math.Max(1, _options.MaxHttpConcurrency));
    }

    public Task<string> GetHtmlAsync(Uri url, bool useBrowser, CancellationToken cancellationToken)
    {
        return useBrowser
            ? _browserService.GetRenderedHtmlAsync(url, cancellationToken)
            : GetStringAsync(url, cancellationToken);
    }

    public async Task<string> GetStringAsync(Uri url, CancellationToken cancellationToken)
    {
        await _httpConcurrencyGate.WaitAsync(cancellationToken);
        try
        {
            using var request = BuildRequest(url);
            using var response = await CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().WaitAsync(cancellationToken);
        }
        finally
        {
            _httpConcurrencyGate.Release();
        }
    }

    public async Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken)
    {
        await _httpConcurrencyGate.WaitAsync(cancellationToken);
        try
        {
            using var request = BuildRequest(url);
            using var response = await CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync().WaitAsync(cancellationToken);
        }
        finally
        {
            _httpConcurrencyGate.Release();
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        return client;
    }

    private static HttpRequestMessage BuildRequest(Uri url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri($"{url.Scheme}://{url.Host}/");
        return request;
    }
}
