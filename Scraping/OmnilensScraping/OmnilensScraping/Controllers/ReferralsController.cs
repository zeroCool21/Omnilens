using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;
using OmnilensScraping.Tracking;

namespace OmnilensScraping.Controllers;

[ApiController]
[Route("api/referrals")]
public sealed class ReferralsController : ControllerBase
{
    private readonly OmnilensDbContext _dbContext;
    private readonly ReferralLinkTokenService _tokenService;

    public ReferralsController(OmnilensDbContext dbContext, ReferralLinkTokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    [HttpPost("links")]
    public async Task<ActionResult<ReferralLinkResponse>> CreateLink(
        [FromBody] CreateReferralLinkRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OfferId == Guid.Empty)
        {
            return BadRequest("OfferId obbligatorio.");
        }

        var offer = await _dbContext.ProductOffers
            .AsNoTracking()
            .Include(item => item.SourceProduct)
                .ThenInclude(item => item.Source)
            .Include(item => item.SourceProduct)
                .ThenInclude(item => item.CanonicalProduct)
            .SingleOrDefaultAsync(item => item.Id == request.OfferId, cancellationToken);

        if (offer is null)
        {
            return NotFound("Offerta non trovata.");
        }

        if (offer.SourceProduct.CanonicalProductId is null)
        {
            return BadRequest("Offerta non collegata a prodotto canonico.");
        }

        var now = DateTimeOffset.UtcNow;
        var referralToken = _tokenService.Create(offer.Id, request.UtmSource, request.UtmCampaign, now);
        var trackedUrl = $"{Request.Scheme}://{Request.Host}/api/referrals/c/{Uri.EscapeDataString(referralToken.Token)}";

        return Ok(new ReferralLinkResponse
        {
            Token = referralToken.Token,
            TrackedUrl = trackedUrl,
            ExpiresAtUtc = referralToken.ExpiresAtUtc,
            OfferId = offer.Id,
            CanonicalProductId = offer.SourceProduct.CanonicalProductId.Value,
            ProductTitle = offer.SourceProduct.CanonicalProduct?.Title ?? "Prodotto",
            SourceId = offer.SourceProduct.SourceId,
            RetailerCode = offer.SourceProduct.Source.RetailerCode,
            RetailerName = offer.SourceProduct.Source.DisplayName,
            OfferUrl = offer.OfferUrl
        });
    }

    [AllowAnonymous]
    [HttpGet("c/{token}")]
    public async Task<IActionResult> Click(string token, CancellationToken cancellationToken)
    {
        if (!_tokenService.TryParse(token, DateTimeOffset.UtcNow, out var payload, out _))
        {
            return NotFound();
        }

        var offer = await _dbContext.ProductOffers
            .AsNoTracking()
            .Include(item => item.SourceProduct)
                .ThenInclude(item => item.Source)
            .SingleOrDefaultAsync(item => item.Id == payload.OfferId, cancellationToken);

        if (offer is null || offer.SourceProduct.CanonicalProductId is null)
        {
            return NotFound();
        }

        var userId = User.TryGetUserId();
        var redirectUrl = AppendUtmParams(offer.OfferUrl, payload.UtmSource, payload.UtmCampaign);

        _dbContext.ClickEvents.Add(new ClickEvent
        {
            UserId = userId,
            CanonicalProductId = offer.SourceProduct.CanonicalProductId.Value,
            SourceId = offer.SourceProduct.SourceId,
            OfferUrl = redirectUrl,
            UtmSource = payload.UtmSource,
            UtmCampaign = payload.UtmCampaign,
            ClickedAtUtc = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Redirect(redirectUrl);
    }

    [Authorize]
    [HttpGet("dashboard")]
    public async Task<ActionResult<ReferralDashboardResponse>> Dashboard(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var totalClicks = await _dbContext.ClickEvents
            .AsNoTracking()
            .CountAsync(item => item.UserId == userId.Value, cancellationToken);

        var conversionsQuery = _dbContext.ConversionEvents
            .AsNoTracking()
            .Where(item => item.ClickEvent != null && item.ClickEvent.UserId == userId.Value);

        // SQLite provider: decimal SUM is not translated, aggregate in-memory.
        var conversionRows = await conversionsQuery
            .Select(item => new
            {
                item.ClickEventId,
                item.SourceId,
                item.Source.RetailerCode,
                RetailerName = item.Source.DisplayName,
                item.CommissionAmount,
                item.Currency
            })
            .ToListAsync(cancellationToken);

        var totalConversions = conversionRows.Count;
        var totalCommission = conversionRows.Sum(item => item.CommissionAmount ?? 0m);
        var currency = conversionRows
            .Select(item => item.Currency)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));

        var clickStats = await _dbContext.ClickEvents
            .AsNoTracking()
            .Where(item => item.UserId == userId.Value)
            .GroupBy(item => new { item.SourceId, item.Source.RetailerCode, item.Source.DisplayName })
            .Select(group => new
            {
                group.Key.SourceId,
                group.Key.RetailerCode,
                RetailerName = group.Key.DisplayName,
                Clicks = group.Count()
            })
            .ToListAsync(cancellationToken);

        var conversionStats = conversionRows
            .GroupBy(item => new { item.SourceId, item.RetailerCode, item.RetailerName })
            .Select(group => new
            {
                group.Key.SourceId,
                group.Key.RetailerCode,
                group.Key.RetailerName,
                Conversions = group.Count(),
                Commission = group.Sum(item => item.CommissionAmount ?? 0m)
            })
            .ToList();

        var conversionMap = conversionStats.ToDictionary(item => item.SourceId);

        var bySource = clickStats
            .Select(item =>
            {
                conversionMap.TryGetValue(item.SourceId, out var conversion);
                return new ReferralSourceStatResponse
                {
                    SourceId = item.SourceId,
                    RetailerCode = item.RetailerCode,
                    RetailerName = item.RetailerName,
                    Clicks = item.Clicks,
                    Conversions = conversion?.Conversions ?? 0,
                    Commission = conversion?.Commission ?? 0m
                };
            })
            .OrderByDescending(item => item.Clicks)
            .ToList();

        // SQLite provider: DateTimeOffset ordering is not translated, order in-memory.
        var clickCandidates = await _dbContext.ClickEvents
            .AsNoTracking()
            .Where(item => item.UserId == userId.Value)
            .Select(item => new { item.Id, item.ClickedAtUtc })
            .ToListAsync(cancellationToken);

        var recentClickIds = clickCandidates
            .OrderByDescending(item => item.ClickedAtUtc)
            .Take(50)
            .Select(item => item.Id)
            .ToList();

        var recentClickDetails = recentClickIds.Count == 0
            ? new List<RecentClickSnapshot>()
            : await _dbContext.ClickEvents
                .AsNoTracking()
                .Where(item => recentClickIds.Contains(item.Id))
                .Select(item => new RecentClickSnapshot
                {
                    ClickId = item.Id,
                    ClickedAtUtc = item.ClickedAtUtc,
                    CanonicalProductId = item.CanonicalProductId,
                    ProductTitle = item.CanonicalProduct.Title,
                    SourceId = item.SourceId,
                    RetailerName = item.Source.DisplayName,
                    OfferUrl = item.OfferUrl
                })
                .ToListAsync(cancellationToken);

        var conversionCounts = conversionRows
            .Where(item => item.ClickEventId.HasValue)
            .GroupBy(item => item.ClickEventId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var conversionCommission = conversionRows
            .Where(item => item.ClickEventId.HasValue)
            .GroupBy(item => item.ClickEventId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.CommissionAmount ?? 0m));

        var conversionCurrency = conversionRows
            .Where(item => item.ClickEventId.HasValue)
            .GroupBy(item => item.ClickEventId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Currency).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)));

        var recentClicks = recentClickDetails
            .OrderByDescending(item => item.ClickedAtUtc)
            .Select(item =>
            {
                conversionCounts.TryGetValue(item.ClickId, out var count);
                conversionCommission.TryGetValue(item.ClickId, out var commission);
                conversionCurrency.TryGetValue(item.ClickId, out var clickCurrency);

                return new ReferralRecentClickResponse
                {
                    ClickId = item.ClickId,
                    ClickedAtUtc = item.ClickedAtUtc,
                    CanonicalProductId = item.CanonicalProductId,
                    ProductTitle = item.ProductTitle,
                    SourceId = item.SourceId,
                    RetailerName = item.RetailerName,
                    OfferUrl = item.OfferUrl,
                    ConversionCount = count,
                    CommissionAmount = commission,
                    Currency = clickCurrency
                };
            })
            .ToList();

        return Ok(new ReferralDashboardResponse
        {
            TotalClicks = totalClicks,
            TotalConversions = totalConversions,
            TotalCommission = totalCommission,
            Currency = currency,
            BySource = bySource,
            RecentClicks = recentClicks
        });
    }

    private sealed class RecentClickSnapshot
    {
        public Guid ClickId { get; set; }
        public DateTimeOffset ClickedAtUtc { get; set; }
        public Guid CanonicalProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public Guid SourceId { get; set; }
        public string RetailerName { get; set; } = string.Empty;
        public string OfferUrl { get; set; } = string.Empty;
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("conversions")]
    public async Task<ActionResult<ConversionEventResponse>> CreateConversion(
        [FromBody] CreateConversionEventRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ClickEventId is null && request.SourceId is null)
        {
            return BadRequest("ClickEventId o SourceId obbligatorio.");
        }

        Guid sourceId;
        if (request.ClickEventId is not null)
        {
            var click = await _dbContext.ClickEvents
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.ClickEventId.Value, cancellationToken);

            if (click is null)
            {
                return NotFound("Click event non trovato.");
            }

            sourceId = click.SourceId;
        }
        else
        {
            sourceId = request.SourceId!.Value;

            var sourceExists = await _dbContext.Sources
                .AsNoTracking()
                .AnyAsync(item => item.Id == sourceId, cancellationToken);

            if (!sourceExists)
            {
                return NotFound("Source non trovata.");
            }
        }

        var entity = new ConversionEvent
        {
            ClickEventId = request.ClickEventId,
            SourceId = sourceId,
            ExternalOrderRef = string.IsNullOrWhiteSpace(request.ExternalOrderRef) ? null : request.ExternalOrderRef.Trim(),
            CommissionAmount = request.CommissionAmount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim().ToUpperInvariant(),
            ConvertedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.ConversionEvents.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ConversionEventResponse
        {
            Id = entity.Id,
            ClickEventId = entity.ClickEventId,
            SourceId = entity.SourceId,
            ExternalOrderRef = entity.ExternalOrderRef,
            CommissionAmount = entity.CommissionAmount,
            Currency = entity.Currency,
            ConvertedAtUtc = entity.ConvertedAtUtc
        });
    }

    private static string AppendUtmParams(string url, string? utmSource, string? utmCampaign)
    {
        var current = url;
        if (!string.IsNullOrWhiteSpace(utmSource) && !ContainsQueryKey(current, "utm_source"))
        {
            current = QueryHelpers.AddQueryString(current, "utm_source", utmSource.Trim());
        }

        if (!string.IsNullOrWhiteSpace(utmCampaign) && !ContainsQueryKey(current, "utm_campaign"))
        {
            current = QueryHelpers.AddQueryString(current, "utm_campaign", utmCampaign.Trim());
        }

        return current;
    }

    private static bool ContainsQueryKey(string url, string key)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Contains($"{key}=", StringComparison.OrdinalIgnoreCase);
        }

        var parsed = QueryHelpers.ParseQuery(uri.Query);
        return parsed.ContainsKey(key);
    }
}
