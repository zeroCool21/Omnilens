using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace OmnilensScraping.Scraping;

public class PlaywrightBrowserService : IAsyncDisposable
{
    private readonly ScrapingOptions _options;
    private readonly ILogger<PlaywrightBrowserService> _logger;
    private readonly SemaphoreSlim _browserConcurrencyGate;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private readonly ConcurrentDictionary<string, Lazy<Task<IBrowserContext>>> _contexts = new(StringComparer.OrdinalIgnoreCase);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightBrowserService(
        IOptions<ScrapingOptions> options,
        ILogger<PlaywrightBrowserService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _browserConcurrencyGate = new SemaphoreSlim(
            Math.Max(1, _options.MaxBrowserConcurrency),
            Math.Max(1, _options.MaxBrowserConcurrency));
    }

    public async Task<string> GetRenderedHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        await _browserConcurrencyGate.WaitAsync(cancellationToken);
        try
        {
            var context = await EnsureContextAsync(url, cancellationToken);
            var page = await context.NewPageAsync();
            await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
            {
                ["Accept-Language"] = "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7",
                ["Cache-Control"] = "max-age=0",
                ["Pragma"] = "no-cache",
                ["Upgrade-Insecure-Requests"] = "1"
            });

            try
            {
                await page.GotoAsync(url.ToString(), new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = _options.BrowserTimeoutMs
                });

                cancellationToken.ThrowIfCancellationRequested();
                await page.WaitForTimeoutAsync(Math.Max(250, _options.BrowserRenderDelayMs));
                return await page.ContentAsync();
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        finally
        {
            _browserConcurrencyGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var contextFactory in _contexts.Values)
        {
            if (!contextFactory.IsValueCreated)
            {
                continue;
            }

            var context = await contextFactory.Value;
            await context.DisposeAsync();
        }

        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        _initializationGate.Dispose();
        _browserConcurrencyGate.Dispose();
    }

    private Task<IBrowserContext> EnsureContextAsync(Uri url, CancellationToken cancellationToken)
    {
        var hostKey = url.Host.ToLowerInvariant();
        var contextFactory = _contexts.GetOrAdd(
            hostKey,
            _ => new Lazy<Task<IBrowserContext>>(
                () => CreateContextAsync(cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return contextFactory.Value;
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
            _browser = await LaunchBrowserAsync(cancellationToken);

            return _browser;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private async Task<IBrowser> LaunchBrowserAsync(CancellationToken cancellationToken)
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = _options.BrowserHeadless,
            Channel = string.IsNullOrWhiteSpace(_options.BrowserChannel)
                ? null
                : _options.BrowserChannel,
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--disable-dev-shm-usage",
                "--disable-infobars",
                "--disable-popup-blocking",
                "--no-default-browser-check",
                "--no-first-run"
            ]
        };

        try
        {
            return await _playwright!.Chromium.LaunchAsync(launchOptions);
        }
        catch (PlaywrightException exception) when (!string.IsNullOrWhiteSpace(launchOptions.Channel))
        {
            _logger.LogWarning(
                exception,
                "Impossibile avviare Playwright con channel {Channel}. Fallback su Chromium bundled.",
                launchOptions.Channel);

            launchOptions.Channel = null;
            return await _playwright!.Chromium.LaunchAsync(launchOptions);
        }
    }

    private async Task<IBrowserContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var browser = await EnsureBrowserAsync(cancellationToken);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ColorScheme = ColorScheme.Light,
            DeviceScaleFactor = 1,
            IgnoreHTTPSErrors = true,
            IsMobile = false,
            JavaScriptEnabled = true,
            Locale = "it-IT",
            ReducedMotion = ReducedMotion.NoPreference,
            ScreenSize = new ScreenSize
            {
                Width = Math.Max(1024, _options.BrowserViewportWidth),
                Height = Math.Max(720, _options.BrowserViewportHeight)
            },
            TimezoneId = "Europe/Rome",
            UserAgent = _options.UserAgent,
            ViewportSize = new ViewportSize
            {
                Width = Math.Max(1024, _options.BrowserViewportWidth),
                Height = Math.Max(720, _options.BrowserViewportHeight)
            }
        });

        await context.AddInitScriptAsync(
            """
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            Object.defineProperty(navigator, 'languages', { get: () => ['it-IT', 'it', 'en-US', 'en'] });
            Object.defineProperty(navigator, 'platform', { get: () => 'Win32' });
            Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
            window.chrome = window.chrome || { runtime: {} };
            """);

        return context;
    }
}
