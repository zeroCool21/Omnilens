using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace OmnilensScraping.Scraping;

public class PlaywrightBrowserService : IAsyncDisposable
{
    private readonly ScrapingOptions _options;
    private readonly SemaphoreSlim _browserConcurrencyGate;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightBrowserService(IOptions<ScrapingOptions> options)
    {
        _options = options.Value;
        _browserConcurrencyGate = new SemaphoreSlim(
            Math.Max(1, _options.MaxBrowserConcurrency),
            Math.Max(1, _options.MaxBrowserConcurrency));
    }

    public async Task<string> GetRenderedHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        await _browserConcurrencyGate.WaitAsync(cancellationToken);
        try
        {
            var browser = await EnsureBrowserAsync(cancellationToken);
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "it-IT",
                UserAgent = _options.UserAgent
            });

            var page = await context.NewPageAsync();
            await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
            {
                ["Accept-Language"] = "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7"
            });

            await page.GotoAsync(url.ToString(), new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _options.BrowserTimeoutMs
            });

            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForTimeoutAsync(500);
            return await page.ContentAsync();
        }
        finally
        {
            _browserConcurrencyGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        _initializationGate.Dispose();
        _browserConcurrencyGate.Dispose();
    }

    private async Task<IBrowser> EnsureBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is not null)
        {
            return _browser;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_browser is not null)
            {
                return _browser;
            }

            _playwright ??= await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            return _browser;
        }
        finally
        {
            _initializationGate.Release();
        }
    }
}
