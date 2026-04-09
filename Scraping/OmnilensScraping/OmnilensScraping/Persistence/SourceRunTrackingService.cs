using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Persistence.Entities;
using OmnilensScraping.Scraping;

namespace OmnilensScraping.Persistence;

public sealed class SourceRunTrackingService
{
    private readonly OmnilensDbContext _dbContext;

    public SourceRunTrackingService(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SourceRun> StartAsync(
        RetailerDefinition definition,
        string runType,
        string? message,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.Sources
            .SingleAsync(item => item.RetailerCode == definition.Retailer.ToString(), cancellationToken);

        var sourceRun = new SourceRun
        {
            SourceId = source.Id,
            RunType = runType,
            Status = "Running",
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.SourceRuns.Add(sourceRun);

        if (!string.IsNullOrWhiteSpace(message))
        {
            _dbContext.SourceRunLogs.Add(new SourceRunLog
            {
                SourceRun = sourceRun,
                Level = "Info",
                Message = message
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return sourceRun;
    }

    public async Task CompleteAsync(
        Guid sourceRunId,
        bool succeeded,
        int itemsFound,
        int itemsSaved,
        string? errorText,
        IEnumerable<string>? warnings,
        CancellationToken cancellationToken)
    {
        var sourceRun = await _dbContext.SourceRuns
            .Include(item => item.Source)
            .SingleAsync(item => item.Id == sourceRunId, cancellationToken);

        sourceRun.Status = succeeded ? "Succeeded" : "Failed";
        sourceRun.ItemsFound = itemsFound;
        sourceRun.ItemsSaved = itemsSaved;
        sourceRun.ErrorText = errorText;
        sourceRun.FinishedAtUtc = DateTimeOffset.UtcNow;

        sourceRun.Source.LastRunAtUtc = sourceRun.FinishedAtUtc;

        if (succeeded)
        {
            sourceRun.Source.LastSuccessfulRunAtUtc = sourceRun.FinishedAtUtc;
            sourceRun.Source.ConsecutiveFailureCount = 0;
            sourceRun.Source.HealthScore = sourceRun.Source.IsEnabled ? 100m : 0m;
        }
        else
        {
            sourceRun.Source.LastFailedRunAtUtc = sourceRun.FinishedAtUtc;
            sourceRun.Source.ConsecutiveFailureCount += 1;
            sourceRun.Source.HealthScore = CalculateHealthScore(sourceRun.Source);
        }

        foreach (var warning in warnings?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct() ?? Array.Empty<string>())
        {
            _dbContext.SourceRunLogs.Add(new SourceRunLog
            {
                SourceRunId = sourceRun.Id,
                Level = "Warning",
                Message = warning
            });
        }

        if (!string.IsNullOrWhiteSpace(errorText))
        {
            _dbContext.SourceRunLogs.Add(new SourceRunLog
            {
                SourceRunId = sourceRun.Id,
                Level = "Error",
                Message = errorText
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static decimal CalculateHealthScore(Source source)
    {
        if (!source.IsEnabled)
        {
            return 0m;
        }

        var baseScore = 100m - Math.Min(80m, source.ConsecutiveFailureCount * 20m);
        if (source.LastSuccessfulRunAtUtc is null)
        {
            baseScore = Math.Min(baseScore, 40m);
        }

        return Math.Max(baseScore, 5m);
    }
}
