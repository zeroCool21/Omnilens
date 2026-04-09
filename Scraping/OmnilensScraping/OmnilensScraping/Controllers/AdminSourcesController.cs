using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[Authorize(Roles = "ADMIN")]
[ApiController]
[Route("api/admin/sources")]
public sealed class AdminSourcesController : ControllerBase
{
    private readonly OmnilensDbContext _dbContext;

    public AdminSourcesController(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SourceAdminItemResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var sources = await _dbContext.Sources
            .AsNoTracking()
            .Include(item => item.SourceRuns)
            .ToListAsync(cancellationToken);

        return Ok(sources
            .OrderByDescending(item => item.PriorityScore)
            .ThenBy(item => item.DisplayName)
            .Select(MapSource)
            .ToArray());
    }

    [HttpGet("health")]
    public async Task<ActionResult<SourceHealthSummaryResponse>> GetHealth(CancellationToken cancellationToken)
    {
        var sources = await _dbContext.Sources
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Ok(new SourceHealthSummaryResponse
        {
            TotalSources = sources.Count,
            HealthySources = sources.Count(item => GetHealthStatus(item) == "Healthy"),
            DegradedSources = sources.Count(item => GetHealthStatus(item) == "Degraded"),
            UnhealthySources = sources.Count(item => GetHealthStatus(item) == "Unhealthy"),
            DisabledSources = sources.Count(item => GetHealthStatus(item) == "Disabled")
        });
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<SourceAdminItemResponse>> Update(
        Guid id,
        [FromBody] UpdateSourceAdminRequest request,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.Sources
            .Include(item => item.SourceRuns)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (source is null)
        {
            return NotFound();
        }

        if (request.SupportsApiCollection.HasValue)
        {
            source.SupportsApiCollection = request.SupportsApiCollection.Value;
        }

        if (request.SupportsScrapingCollection.HasValue)
        {
            source.SupportsScrapingCollection = request.SupportsScrapingCollection.Value;
        }

        if (request.SupportsManualCollection.HasValue)
        {
            source.SupportsManualCollection = request.SupportsManualCollection.Value;
        }

        if (request.IsEnabled.HasValue)
        {
            source.IsEnabled = request.IsEnabled.Value;
            if (!source.IsEnabled)
            {
                source.HealthScore = 0m;
            }
        }

        if (request.PriorityScore.HasValue)
        {
            source.PriorityScore = Math.Clamp(request.PriorityScore.Value, 0, 1000);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapSource(source));
    }

    private static SourceAdminItemResponse MapSource(Source source)
    {
        var totalRuns = source.SourceRuns.Count;
        var successfulRuns = source.SourceRuns.Count(item => item.Status == "Succeeded");
        var failedRuns = source.SourceRuns.Count(item => item.Status == "Failed");

        return new SourceAdminItemResponse
        {
            Id = source.Id,
            RetailerCode = source.RetailerCode,
            DisplayName = source.DisplayName,
            Category = source.Category,
            CountryCode = source.CountryCode,
            BaseUrl = source.BaseUrl,
            SupportsCatalogBootstrap = source.SupportsCatalogBootstrap,
            SupportsLiveScrape = source.SupportsLiveScrape,
            SupportsApiCollection = source.SupportsApiCollection,
            SupportsScrapingCollection = source.SupportsScrapingCollection,
            SupportsManualCollection = source.SupportsManualCollection,
            IsEnabled = source.IsEnabled,
            PriorityScore = source.PriorityScore,
            HealthScore = source.HealthScore,
            ConsecutiveFailureCount = source.ConsecutiveFailureCount,
            HealthStatus = GetHealthStatus(source),
            LastRunAtUtc = source.LastRunAtUtc,
            LastSuccessfulRunAtUtc = source.LastSuccessfulRunAtUtc,
            LastFailedRunAtUtc = source.LastFailedRunAtUtc,
            TotalRuns = totalRuns,
            SuccessfulRuns = successfulRuns,
            FailedRuns = failedRuns
        };
    }

    private static string GetHealthStatus(Source source)
    {
        if (!source.IsEnabled)
        {
            return "Disabled";
        }

        if (source.HealthScore >= 80m)
        {
            return "Healthy";
        }

        if (source.HealthScore >= 50m)
        {
            return "Degraded";
        }

        return "Unhealthy";
    }
}
