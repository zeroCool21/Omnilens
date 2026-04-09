using Microsoft.Extensions.Options;

namespace OmnilensScraping.Tracking;

public sealed class AlertEvaluationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlertingOptions _options;
    private readonly ILogger<AlertEvaluationHostedService> _logger;

    public AlertEvaluationHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AlertingOptions> options,
        ILogger<AlertEvaluationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Alert worker disabilitato.");
            return;
        }

        if (_options.RunOnStartup)
        {
            var initialDelay = Math.Max(0, _options.InitialDelaySeconds);
            if (initialDelay > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(initialDelay), stoppingToken);
            }

            await RunOnceAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.CheckIntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<AlertEvaluationService>();

        try
        {
            var result = await evaluator.EvaluateAsync(cancellationToken);
            if (result.DeliveriesCreated > 0)
            {
                _logger.LogInformation(
                    "Alert evaluation completata. Rules: {RulesEvaluated}, deliveries: {DeliveriesCreated}",
                    result.RulesEvaluated,
                    result.DeliveriesCreated);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Alert evaluation fallita.");
        }
    }
}

