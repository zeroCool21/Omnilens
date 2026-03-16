using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OmnilensScraping.Models;
using OmnilensScraping.Scraping;

namespace OmnilensScraping.Controllers;

/// <summary>
/// Endpoint REST per sitemap, benchmark di concorrenza e scraping prodotto.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ScrapingController : ControllerBase
{
    private readonly ScrapingCoordinator _scrapingCoordinator;
    private readonly ParallelScrapingService _parallelScrapingService;
    private readonly CatalogDiscoveryService _catalogDiscoveryService;
    private readonly AmazonCatalogBootstrapService _amazonCatalogBootstrapService;
    private readonly RetailerCatalogBootstrapService _retailerCatalogBootstrapService;
    private readonly RetailerRegistry _retailerRegistry;

    public ScrapingController(
        ScrapingCoordinator scrapingCoordinator,
        ParallelScrapingService parallelScrapingService,
        CatalogDiscoveryService catalogDiscoveryService,
        AmazonCatalogBootstrapService amazonCatalogBootstrapService,
        RetailerCatalogBootstrapService retailerCatalogBootstrapService,
        RetailerRegistry retailerRegistry)
    {
        _scrapingCoordinator = scrapingCoordinator;
        _parallelScrapingService = parallelScrapingService;
        _catalogDiscoveryService = catalogDiscoveryService;
        _amazonCatalogBootstrapService = amazonCatalogBootstrapService;
        _retailerCatalogBootstrapService = retailerCatalogBootstrapService;
        _retailerRegistry = retailerRegistry;
    }

    /// <summary>
    /// Verifica rapida dello stato del backend di scraping.
    /// </summary>
    /// <remarks>
    /// Restituisce lo stato del servizio, il timestamp UTC corrente e l'elenco sintetico degli endpoint principali.
    /// </remarks>
    /// <response code="200">Servizio operativo.</response>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "ok",
            utc = DateTimeOffset.UtcNow,
            swagger = "/swagger",
            endpoints = new[]
            {
                "GET /api/scraping/retailers",
                "GET /api/scraping/retailers/pharmacies",
                "GET /api/scraping/samples/{retailer}",
                "GET /api/scraping/catalog/{retailer}/count",
                "GET /api/scraping/catalog/{retailer}/parsed",
                "GET /api/scraping/catalog/{retailer}/concurrency",
                "POST /api/scraping/catalog/{retailer}/bootstrap",
                "GET /api/scraping/product",
                "POST /api/scraping/product"
            }
        });
    }

    /// <summary>
    /// Elenca i retailer supportati dal backend.
    /// </summary>
    /// <remarks>
    /// Utile per sapere quali host, sitemap e modalita di scraping sono disponibili.
    /// </remarks>
    /// <response code="200">Elenco dei retailer registrati.</response>
    [HttpGet("retailers")]
    public ActionResult<IReadOnlyCollection<RetailerInfo>> GetRetailers()
    {
        return Ok(_scrapingCoordinator.GetRetailers());
    }

    /// <summary>
    /// Elenca solo i retailer farmacia supportati dal backend.
    /// </summary>
    /// <remarks>
    /// Utile per lavorare sul vertical farmacie senza dover filtrare l'elenco completo dei retailer.
    /// </remarks>
    /// <response code="200">Elenco dei retailer farmacia registrati.</response>
    [HttpGet("retailers/pharmacies")]
    public ActionResult<IReadOnlyCollection<RetailerInfo>> GetPharmacyRetailers()
    {
        return Ok(_scrapingCoordinator.GetRetailers(RetailerCategory.Pharmacy));
    }

    /// <summary>
    /// Restituisce alcuni URL prodotto presi dal sitemap del retailer.
    /// </summary>
    /// <param name="retailer">Retailer da interrogare.</param>
    /// <param name="take">Numero di URL campione da restituire.</param>
    /// <param name="cancellationToken">Token per annullare la richiesta lato server.</param>
    /// <remarks>
    /// Serve per vedere velocemente che URL il backend considera validi prima di lanciare scraping o benchmark.
    /// </remarks>
    /// <response code="200">Campioni URL trovati nel sitemap.</response>
    [HttpGet("samples/{retailer}")]
    public async Task<ActionResult<IReadOnlyCollection<string>>> GetSamples(
        RetailerType retailer,
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 20);
        try
        {
            var samples = await _catalogDiscoveryService.GetSampleProductUrlsAsync(retailer, take, cancellationToken);
            return Ok(samples);
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Conta quanti prodotti sono presenti nel catalogo sitemap del retailer.
    /// </summary>
    /// <param name="retailer">Retailer di cui vuoi conoscere la dimensione del catalogo.</param>
    /// <param name="cancellationToken">Token per annullare la richiesta lato server.</param>
    /// <remarks>
    /// Legge i product sitemap del retailer e somma il numero totale di URL prodotto trovati.
    /// </remarks>
    /// <response code="200">Conteggio del catalogo e numero di sitemap prodotto.</response>
    [HttpGet("catalog/{retailer}/count")]
    public async Task<ActionResult<RetailerCatalogCountResponse>> GetCatalogCount(
        RetailerType retailer,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var retailers = _scrapingCoordinator.GetRetailers();
            var retailerInfo = retailers.First(info => info.Retailer == retailer);
            var coverage = _catalogDiscoveryService.GetCoverageStatus(retailer);
            var totalProducts = await _catalogDiscoveryService.CountProductUrlsAsync(retailer, cancellationToken);
            var productSitemaps = await _catalogDiscoveryService.CountProductSourcesAsync(retailer, cancellationToken);

            return Ok(new RetailerCatalogCountResponse
            {
                Retailer = retailer,
                SitemapIndexUrl = retailerInfo.SitemapIndexUrl,
                ProductSitemaps = productSitemaps,
                TotalProducts = totalProducts,
                CatalogCoverage = coverage
            });
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Estrae e parsa i primi prodotti del catalogo del retailer.
    /// </summary>
    /// <param name="retailer">Retailer da cui prelevare i prodotti iniziali del sitemap.</param>
    /// <param name="take">Numero di prodotti da prelevare e parsare.</param>
    /// <param name="mode">Tecnica di scraping da usare: Auto, Html, JsonLd, EmbeddedState o Browser.</param>
    /// <param name="maxConcurrency">Numero massimo di prodotti da elaborare in parallelo durante il batch. Se 0 usa il profilo automatico configurato.</param>
    /// <param name="cancellationToken">Token per annullare la richiesta lato server.</param>
    /// <remarks>
    /// La risposta include anche il tempo totale della chiamata in millisecondi, secondi e formato leggibile.
    /// </remarks>
    /// <response code="200">Batch eseguito con elenco dei prodotti parsati.</response>
    /// <response code="400">Parametri non validi.</response>
    [HttpGet("catalog/{retailer}/parsed")]
    public async Task<ActionResult<RetailerParsedProductsResponse>> GetParsedCatalogProducts(
        RetailerType retailer,
        [FromQuery] int take = 5,
        [FromQuery] ScrapingMode mode = ScrapingMode.Auto,
        [FromQuery] int maxConcurrency = 0,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (take <= 0)
        {
            return BadRequest("Il parametro take deve essere maggiore di 0.");
        }

        if (maxConcurrency < 0)
        {
            return BadRequest("Il parametro maxConcurrency deve essere maggiore o uguale a 0.");
        }

        try
        {
            var coverage = _catalogDiscoveryService.GetCoverageStatus(retailer);
            var sampleUrls = await _catalogDiscoveryService.GetSampleProductUrlsAsync(retailer, take, cancellationToken);
            var appliedMaxConcurrency = _parallelScrapingService.ResolveBatchConcurrency(maxConcurrency);
            var parsedProducts = await _parallelScrapingService.ScrapeManyAsync(
                retailer,
                sampleUrls,
                mode,
                maxConcurrency,
                cancellationToken);
            stopwatch.Stop();

            return Ok(new RetailerParsedProductsResponse
            {
                Retailer = retailer,
                RequestedTake = take,
                Returned = parsedProducts.Count,
                SuccessCount = parsedProducts.Count(item => item.Success),
                FailedCount = parsedProducts.Count(item => !item.Success),
                RequestedMode = mode,
                AppliedMaxConcurrency = appliedMaxConcurrency,
                CatalogCoverage = coverage,
                ElapsedMs = (long)Math.Round(stopwatch.Elapsed.TotalMilliseconds),
                ElapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
                ElapsedDisplay = FormatElapsed(stopwatch.Elapsed),
                Products = parsedProducts
            });
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Misura la concorrenza massima osservata prima di vedere instabilita, throttle o segnali di blocco.
    /// </summary>
    /// <param name="retailer">Retailer su cui eseguire il benchmark.</param>
    /// <param name="sampleSize">Numero di URL campione da usare per i test di stabilita.</param>
    /// <param name="startConcurrency">Primo livello di parallelismo da testare.</param>
    /// <param name="maxConcurrency">Livello massimo di parallelismo da testare.</param>
    /// <param name="mode">Tecnica di scraping da usare durante il benchmark.</param>
    /// <param name="successRateThreshold">Soglia minima di successi, tra 0 e 1, per considerare stabile un livello.</param>
    /// <param name="cancellationToken">Token per annullare la richiesta lato server.</param>
    /// <remarks>
    /// Il risultato e osservazionale: restituisce il livello consigliato in base ai probe eseguiti, ma non garantisce assenza di blocchi futuri.
    /// </remarks>
    /// <response code="200">Benchmark completato con raccomandazione e metriche per probe.</response>
    /// <response code="400">Parametri non validi.</response>
    /// <response code="404">Nessun prodotto trovato nei sitemap del retailer.</response>
    [HttpGet("catalog/{retailer}/concurrency")]
    public async Task<ActionResult<RetailerConcurrencyBenchmarkResponse>> BenchmarkConcurrency(
        RetailerType retailer,
        [FromQuery] int sampleSize = 24,
        [FromQuery] int startConcurrency = 1,
        [FromQuery] int maxConcurrency = 32,
        [FromQuery] ScrapingMode mode = ScrapingMode.Auto,
        [FromQuery] double successRateThreshold = 0.9,
        CancellationToken cancellationToken = default)
    {
        if (sampleSize <= 0)
        {
            return BadRequest("Il parametro sampleSize deve essere maggiore di 0.");
        }

        if (startConcurrency <= 0)
        {
            return BadRequest("Il parametro startConcurrency deve essere maggiore di 0.");
        }

        if (maxConcurrency < startConcurrency)
        {
            return BadRequest("Il parametro maxConcurrency deve essere maggiore o uguale a startConcurrency.");
        }

        if (successRateThreshold <= 0 || successRateThreshold > 1)
        {
            return BadRequest("Il parametro successRateThreshold deve essere compreso tra 0 e 1.");
        }

        IReadOnlyCollection<string> baseUrls;
        try
        {
            baseUrls = await _catalogDiscoveryService.GetSampleProductUrlsAsync(retailer, sampleSize, cancellationToken);
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(exception.Message);
        }
        if (baseUrls.Count == 0)
        {
            return NotFound("Nessun prodotto trovato nel sitemap del retailer richiesto.");
        }

        var probeMap = new Dictionary<int, ConcurrencyProbeResult>();
        var stopReason = "Raggiunto il massimo richiesto.";
        var recommendedConcurrency = 0;

        var currentConcurrency = startConcurrency;
        var firstFailingConcurrency = 0;

        while (currentConcurrency <= maxConcurrency)
        {
            var probe = await ProbeConcurrencyAsync(
                retailer,
                baseUrls,
                sampleSize,
                currentConcurrency,
                mode,
                cancellationToken);

            probeMap[currentConcurrency] = probe;

            if (IsProbeStable(probe, successRateThreshold))
            {
                recommendedConcurrency = currentConcurrency;
                if (currentConcurrency == maxConcurrency)
                {
                    break;
                }

                currentConcurrency = GetNextCoarseConcurrency(currentConcurrency, maxConcurrency);
                continue;
            }

            firstFailingConcurrency = currentConcurrency;
            stopReason = BuildStopReason(probe, successRateThreshold);
            break;
        }

        if (firstFailingConcurrency > 0 && recommendedConcurrency > 0 && recommendedConcurrency + 1 <= firstFailingConcurrency - 1)
        {
            var low = recommendedConcurrency + 1;
            var high = firstFailingConcurrency - 1;

            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                if (!probeMap.TryGetValue(mid, out var probe))
                {
                    probe = await ProbeConcurrencyAsync(
                        retailer,
                        baseUrls,
                        sampleSize,
                        mid,
                        mode,
                        cancellationToken);

                    probeMap[mid] = probe;
                }

                if (IsProbeStable(probe, successRateThreshold))
                {
                    recommendedConcurrency = mid;
                    low = mid + 1;
                }
                else
                {
                    stopReason = BuildStopReason(probe, successRateThreshold);
                    high = mid - 1;
                }
            }
        }

        if (recommendedConcurrency == 0)
        {
            recommendedConcurrency = startConcurrency;
            stopReason = firstFailingConcurrency == startConcurrency
                ? "Gia il primo livello testato mostra instabilita: usa un valore molto conservativo."
                : stopReason;
        }

        return Ok(new RetailerConcurrencyBenchmarkResponse
        {
            Retailer = retailer,
            RequestedMode = mode,
            SampleSize = sampleSize,
            StartConcurrency = startConcurrency,
            MaxConcurrency = maxConcurrency,
            SuccessRateThreshold = successRateThreshold,
            RecommendedMaxConcurrency = recommendedConcurrency,
            RecommendedMode = mode,
            StopReason = stopReason,
            Notes = "La raccomandazione e osservazionale: misura errori, 403/429/challenge e latenza sui campioni testati, ma non garantisce assenza di blocchi futuri.",
            Probes = probeMap
                .OrderBy(entry => entry.Key)
                .Select(entry => entry.Value)
                .ToArray()
        });
    }

    /// <summary>
    /// Genera una prima snapshot locale del catalogo retailer quando non esiste un sitemap pubblico completo.
    /// </summary>
    /// <param name="retailer">Retailer per cui eseguire il bootstrap. Supporta AmazonIt e i retailer che richiedono crawl pubblico per costruire una snapshot locale.</param>
    /// <param name="force">Se true rigenera i file bootstrap locali rimuovendo quelli precedenti.</param>
    /// <param name="take">Numero massimo di prodotti da salvare nella snapshot. Se 0 non applica limiti.</param>
    /// <param name="cancellationToken">Token per annullare la richiesta lato server.</param>
    /// <remarks>
    /// Per Amazon IT il bootstrap esplora il grafo pubblico di discovery page del sito, non solo le classifiche bestseller,
    /// e salva sitemap XML locali organizzati per categorie riusabili dagli endpoint catalogo.
    /// Per retailer come TopFarmacia o TuttoFarma il bootstrap costruisce una snapshot locale best-effort a partire dalle discovery page pubbliche.
    /// La copertura totale al 100% resta garantibile solo quando il backend legge una snapshot autorevole completa.
    /// </remarks>
    /// <response code="200">Bootstrap completato.</response>
    /// <response code="400">Retailer non supportato o richiesta non valida.</response>
    [HttpPost("catalog/{retailer}/bootstrap")]
    public async Task<ActionResult<RetailerCatalogBootstrapResponse>> BootstrapCatalog(
        RetailerType retailer,
        [FromQuery] bool force = false,
        [FromQuery] int take = 0,
        CancellationToken cancellationToken = default)
    {
        if (take < 0)
        {
            return BadRequest("Il parametro take deve essere maggiore o uguale a 0.");
        }

        var definition = _retailerRegistry.Get(retailer);

        if (retailer == RetailerType.AmazonIt)
        {
            var amazonResponse = await _amazonCatalogBootstrapService.BootstrapAsync(force, take, cancellationToken);
            return Ok(amazonResponse);
        }

        if (_retailerCatalogBootstrapService.CanBootstrap(definition))
        {
            var bootstrapResponse = await _retailerCatalogBootstrapService.BootstrapAsync(definition, force, take, cancellationToken);
            return Ok(bootstrapResponse);
        }

        return BadRequest("Il bootstrap automatico del catalogo e disponibile solo per i retailer che non espongono un product sitemap pubblico completo, ad esempio AmazonIt, TopFarmacia e TuttoFarma.");
    }

    /// <summary>
    /// Esegue lo scraping di un singolo prodotto via query string.
    /// </summary>
    /// <param name="url">URL completo della pagina prodotto da analizzare.</param>
    /// <param name="retailer">Retailer opzionale; se omesso viene dedotto dall'host dell'URL.</param>
    /// <param name="mode">Tecnica di scraping richiesta.</param>
    /// <param name="cancellationToken">Token per annullare la richiesta lato server.</param>
    /// <remarks>
    /// Endpoint comodo per test rapidi direttamente da Swagger senza passare dal body JSON.
    /// </remarks>
    /// <response code="200">Prodotto parsato correttamente.</response>
    /// <response code="400">Richiesta non valida o parsing non riuscito.</response>
    /// <response code="502">Errore HTTP verso il sito target.</response>
    /// <response code="500">Errore interno del backend.</response>
    [HttpGet("product")]
    public Task<ActionResult<ScrapeProductResponse>> GetProduct(
        [FromQuery] string url,
        [FromQuery] RetailerType? retailer = null,
        [FromQuery] ScrapingMode mode = ScrapingMode.Auto,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(new ScrapeProductRequest
        {
            Url = url,
            Retailer = retailer,
            Mode = mode
        }, cancellationToken);
    }

    /// <summary>
    /// Esegue lo scraping di un singolo prodotto via body JSON.
    /// </summary>
    /// <param name="request">Body con URL, retailer opzionale e modalita di scraping.</param>
    /// <param name="cancellationToken">Token per annullare la richiesta lato server.</param>
    /// <remarks>
    /// Versione POST dello scraping prodotto, utile quando vuoi inviare la richiesta in formato JSON.
    /// </remarks>
    /// <response code="200">Prodotto parsato correttamente.</response>
    /// <response code="400">Richiesta non valida o parsing non riuscito.</response>
    /// <response code="502">Errore HTTP verso il sito target.</response>
    /// <response code="500">Errore interno del backend.</response>
    [HttpPost("product")]
    public Task<ActionResult<ScrapeProductResponse>> PostProduct(
        [FromBody] ScrapeProductRequest request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(request, cancellationToken);
    }

    private async Task<ActionResult<ScrapeProductResponse>> ExecuteAsync(
        ScrapeProductRequest request,
        CancellationToken cancellationToken)
    {
        var response = await TryScrapeAsync(request, cancellationToken);
        if (response.Success)
        {
            return Ok(response);
        }

        return response.Message?.StartsWith("Errore HTTP", StringComparison.OrdinalIgnoreCase) == true
            ? StatusCode(StatusCodes.Status502BadGateway, response)
            : response.Message?.StartsWith("Errore interno", StringComparison.OrdinalIgnoreCase) == true
                ? StatusCode(StatusCodes.Status500InternalServerError, response)
                : BadRequest(response);
    }

    private async Task<ScrapeProductResponse> TryScrapeAsync(
        ScrapeProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _scrapingCoordinator.ScrapeAsync(request, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return new ScrapeProductResponse
            {
                Success = false,
                Message = exception.Message,
                Retailer = request.Retailer ?? default,
                RequestedMode = request.Mode
            };
        }
        catch (HttpRequestException exception)
        {
            return new ScrapeProductResponse
            {
                Success = false,
                Message = $"Errore HTTP durante lo scraping: {exception.Message}",
                Retailer = request.Retailer ?? default,
                RequestedMode = request.Mode
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ScrapeProductResponse
            {
                Success = false,
                Message = $"Errore interno: {exception.Message}",
                Retailer = request.Retailer ?? default,
                RequestedMode = request.Mode
            };
        }
    }

    private async Task<ConcurrencyProbeResult> ProbeConcurrencyAsync(
        RetailerType retailer,
        IReadOnlyCollection<string> baseUrls,
        int sampleSize,
        int concurrency,
        ScrapingMode mode,
        CancellationToken cancellationToken)
    {
        var requestCount = Math.Max(sampleSize, concurrency * 2);
        var urls = ExpandUrls(baseUrls, requestCount);
        var responses = await _parallelScrapingService.ScrapeManyAsync(
            retailer,
            urls,
            mode,
            concurrency,
            cancellationToken);

        var durations = responses
            .Select(response => response.DurationMs)
            .OrderBy(value => value)
            .ToArray();

        var banSignals = responses.Count(HasBanSignal);
        var successCount = responses.Count(response => response.Success);
        var failedCount = responses.Count - successCount;

        return new ConcurrencyProbeResult
        {
            Concurrency = concurrency,
            Requests = responses.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            BanSignalCount = banSignals,
            SuccessRate = responses.Count == 0 ? 0 : (double)successCount / responses.Count,
            AverageDurationMs = durations.Length == 0 ? 0 : (long)durations.Average(),
            P95DurationMs = CalculatePercentile(durations, 0.95)
        };
    }

    private static bool IsProbeStable(ConcurrencyProbeResult probe, double successRateThreshold)
    {
        return probe.SuccessRate >= successRateThreshold && probe.BanSignalCount == 0;
    }

    private static string BuildStopReason(ConcurrencyProbeResult probe, double successRateThreshold)
    {
        if (probe.BanSignalCount > 0)
        {
            return $"Rilevati segnali di blocco o throttle al livello {probe.Concurrency}.";
        }

        if (probe.SuccessRate < successRateThreshold)
        {
            return $"Success rate scesa a {probe.SuccessRate:P1} sotto la soglia {successRateThreshold:P0} al livello {probe.Concurrency}.";
        }

        return $"Instabilita rilevata al livello {probe.Concurrency}.";
    }

    private static int GetNextCoarseConcurrency(int currentConcurrency, int maxConcurrency)
    {
        if (currentConcurrency >= maxConcurrency)
        {
            return maxConcurrency;
        }

        if (currentConcurrency < 8)
        {
            return Math.Min(currentConcurrency * 2, maxConcurrency);
        }

        return Math.Min(currentConcurrency * 2, maxConcurrency);
    }

    private static List<string> ExpandUrls(IReadOnlyCollection<string> baseUrls, int requestCount)
    {
        var source = baseUrls.Where(url => !string.IsNullOrWhiteSpace(url)).ToArray();
        if (source.Length == 0 || requestCount <= 0)
        {
            return new List<string>();
        }

        var urls = new List<string>(requestCount);
        for (var index = 0; index < requestCount; index++)
        {
            urls.Add(source[index % source.Length]);
        }

        return urls;
    }

    private static long CalculatePercentile(long[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling((sortedValues.Length * percentile)) - 1;
        index = Math.Clamp(index, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            var parts = new List<string>
            {
                FormatTimePart((int)elapsed.TotalHours, "ora", "ore")
            };

            if (elapsed.Minutes > 0)
            {
                parts.Add(FormatTimePart(elapsed.Minutes, "minuto", "minuti"));
            }

            if (elapsed.Seconds > 0)
            {
                parts.Add(FormatTimePart(elapsed.Seconds, "secondo", "secondi"));
            }

            return string.Join(" ", parts);
        }

        if (elapsed.TotalMinutes >= 1)
        {
            var parts = new List<string>
            {
                FormatTimePart((int)elapsed.TotalMinutes, "minuto", "minuti")
            };

            if (elapsed.Seconds > 0)
            {
                parts.Add(FormatTimePart(elapsed.Seconds, "secondo", "secondi"));
            }

            return string.Join(" ", parts);
        }

        var totalSeconds = Math.Round(elapsed.TotalSeconds, 2);
        return $"{totalSeconds:0.##} {(Math.Abs(totalSeconds - 1) < 0.005 ? "secondo" : "secondi")}";
    }

    private static string FormatTimePart(int value, string singular, string plural)
    {
        return $"{value} {(value == 1 ? singular : plural)}";
    }

    private static bool HasBanSignal(ScrapeProductResponse response)
    {
        var text = $"{response.Message} {string.Join(' ', response.Warnings)}";
        return text.Contains("403", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("429", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("challenge", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("denied", StringComparison.OrdinalIgnoreCase);
    }
}
