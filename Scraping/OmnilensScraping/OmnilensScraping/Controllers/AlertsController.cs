using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[Authorize]
[ApiController]
[Route("api/alerts")]
public sealed class AlertsController : ControllerBase
{
    private readonly OmnilensDbContext _dbContext;

    public AlertsController(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
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
