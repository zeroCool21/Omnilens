using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;
using OmnilensScraping.Tracking;

namespace OmnilensScraping.Controllers;

[Authorize]
[ApiController]
[Route("api/alerts")]
public sealed class AlertsController : ControllerBase
{
    private readonly OmnilensDbContext _dbContext;
    private readonly AlertEvaluationService _alertEvaluationService;

    public AlertsController(OmnilensDbContext dbContext, AlertEvaluationService alertEvaluationService)
    {
        _dbContext = dbContext;
        _alertEvaluationService = alertEvaluationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AlertRuleResponse>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var items = await _dbContext.AlertRules
            .AsNoTracking()
            .Where(item => item.UserId == userId.Value)
            .Select(item => new AlertRuleResponse
            {
                Id = item.Id,
                CanonicalProductId = item.CanonicalProductId,
                ProductTitle = item.CanonicalProduct.Title,
                TargetPrice = item.TargetPrice,
                NotifyOnRestock = item.NotifyOnRestock,
                IsActive = item.IsActive,
                CreatedAtUtc = item.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items.OrderByDescending(item => item.CreatedAtUtc).ToList());
    }

    [HttpGet("deliveries")]
    public async Task<ActionResult<IReadOnlyCollection<AlertDeliveryResponse>>> GetDeliveries(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var deliveries = await _dbContext.AlertDeliveries
            .AsNoTracking()
            .Where(item => item.AlertRule.UserId == userId.Value)
            .Select(item => new AlertDeliveryResponse
            {
                Id = item.Id,
                AlertRuleId = item.AlertRuleId,
                CanonicalProductId = item.AlertRule.CanonicalProductId,
                ProductTitle = item.AlertRule.CanonicalProduct.Title,
                TriggerReason = item.TriggerReason,
                PayloadJson = item.PayloadJson,
                DeliveredAtUtc = item.DeliveredAtUtc
            })
            .ToListAsync(cancellationToken);

        // SQLite provider: DateTimeOffset ordering is not translated, order in-memory.
        return Ok(deliveries
            .OrderByDescending(item => item.DeliveredAtUtc)
            .Take(200)
            .ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AlertRuleResponse>> Create([FromBody] UpsertAlertRequest request, CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var product = await _dbContext.CanonicalProducts
            .SingleOrDefaultAsync(item => item.Id == request.CanonicalProductId, cancellationToken);

        if (product is null)
        {
            return NotFound("Prodotto non trovato.");
        }

        var entity = new AlertRule
        {
            UserId = userId.Value,
            CanonicalProductId = request.CanonicalProductId,
            TargetPrice = request.TargetPrice,
            NotifyOnRestock = request.NotifyOnRestock,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.AlertRules.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(Map(entity, product.Title));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AlertRuleResponse>> SetStatus(
        Guid id,
        [FromBody] UpdateAlertStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var entity = await _dbContext.AlertRules
            .Include(item => item.CanonicalProduct)
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId.Value, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(entity, entity.CanonicalProduct.Title));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AlertRuleResponse>> Update(
        Guid id,
        [FromBody] UpsertAlertRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var entity = await _dbContext.AlertRules
            .Include(item => item.CanonicalProduct)
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId.Value, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        entity.TargetPrice = request.TargetPrice;
        entity.NotifyOnRestock = request.NotifyOnRestock;
        entity.IsActive = true;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(entity, entity.CanonicalProduct.Title));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var entity = await _dbContext.AlertRules
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId.Value, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        _dbContext.AlertRules.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("evaluate")]
    public async Task<ActionResult<AlertEvaluationResult>> Evaluate(CancellationToken cancellationToken)
    {
        var result = await _alertEvaluationService.EvaluateAsync(cancellationToken);
        return Ok(result);
    }

    private static AlertRuleResponse Map(AlertRule entity, string productTitle)
    {
        return new AlertRuleResponse
        {
            Id = entity.Id,
            CanonicalProductId = entity.CanonicalProductId,
            ProductTitle = productTitle,
            TargetPrice = entity.TargetPrice,
            NotifyOnRestock = entity.NotifyOnRestock,
            IsActive = entity.IsActive,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }
}
