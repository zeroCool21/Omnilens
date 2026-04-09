using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[ApiController]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly OmnilensDbContext _dbContext;

    public SubscriptionsController(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("api/plans/families")]
    public async Task<ActionResult<IReadOnlyCollection<ProductFamilyResponse>>> GetFamilies(
        [FromQuery] string? audience,
        CancellationToken cancellationToken)
    {
        var families = await _dbContext.ProductFamilies
            .AsNoTracking()
            .Include(item => item.Plans)
                .ThenInclude(item => item.PlanEntitlements)
                    .ThenInclude(item => item.Entitlement)
            .Where(item => item.IsActive)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(audience))
        {
            families = families
                .Where(item => item.Audience.Equals(audience.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var response = families
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new ProductFamilyResponse
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Audience = item.Audience,
                Description = item.Description,
                Plans = item.Plans
                    .Where(plan => plan.IsActive)
                    .OrderBy(plan => plan.SortOrder)
                    .ThenBy(plan => plan.Name)
                    .Select(plan => new PlanResponse
                    {
                        Id = plan.Id,
                        Code = plan.Code,
                        Name = plan.Name,
                        Audience = plan.Audience,
                        BillingPeriod = plan.BillingPeriod,
                        Price = plan.Price,
                        Currency = plan.Currency,
                        Description = plan.Description,
                        Entitlements = plan.PlanEntitlements
                            .OrderBy(planEntitlement => planEntitlement.Entitlement.Name)
                            .Select(planEntitlement => new PlanEntitlementResponse
                            {
                                Code = planEntitlement.Entitlement.Code,
                                Name = planEntitlement.Entitlement.Name,
                                ValueType = planEntitlement.Entitlement.ValueType,
                                StringValue = planEntitlement.StringValue,
                                NumericValue = planEntitlement.NumericValue,
                                BooleanValue = planEntitlement.BooleanValue
                            })
                            .ToArray()
                    })
                    .ToArray()
            })
            .ToArray();

        return Ok(response);
    }

    [Authorize]
    [HttpGet("api/subscriptions/me")]
    public async Task<ActionResult<IReadOnlyCollection<UserSubscriptionResponse>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var subscriptions = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(item => item.Plan)
                .ThenInclude(item => item.ProductFamily)
            .Where(item => item.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        var response = subscriptions
            .OrderByDescending(item => item.StartedAtUtc)
            .Select(item => MapSubscription(item, item.Plan, item.Plan.ProductFamily))
            .ToArray();

        return Ok(response);
    }

    [Authorize]
    [HttpPost("api/subscriptions/activate-local")]
    public async Task<ActionResult<UserSubscriptionResponse>> ActivateLocal(
        [FromBody] ActivateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.PlanCode))
        {
            return BadRequest("PlanCode obbligatorio.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var plan = await _dbContext.Plans
            .Include(item => item.ProductFamily)
            .SingleOrDefaultAsync(item => item.Code == request.PlanCode.Trim().ToUpperInvariant() && item.IsActive, cancellationToken);

        if (plan is null)
        {
            return NotFound("Piano non trovato.");
        }

        if (!plan.Audience.Equals(user.UserType, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Il piano richiesto non e compatibile con il tipo utente.");
        }

        var subscriptionsToClose = await _dbContext.UserSubscriptions
            .Include(item => item.Plan)
            .Where(item =>
                item.UserId == user.Id &&
                item.Status == "Active" &&
                item.Plan.ProductFamilyId == plan.ProductFamilyId)
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptionsToClose)
        {
            subscription.Status = "Superseded";
            subscription.AutoRenew = false;
            subscription.EndsAtUtc = DateTimeOffset.UtcNow;
            subscription.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        var newSubscription = new UserSubscription
        {
            UserId = user.Id,
            PlanId = plan.Id,
            Status = "Active",
            AutoRenew = request.AutoRenew,
            StartedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.UserSubscriptions.Add(newSubscription);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapSubscription(newSubscription, plan, plan.ProductFamily));
    }

    private static UserSubscriptionResponse MapSubscription(UserSubscription subscription, Plan plan, ProductFamily family)
    {
        return new UserSubscriptionResponse
        {
            Id = subscription.Id,
            Status = subscription.Status,
            AutoRenew = subscription.AutoRenew,
            StartedAtUtc = subscription.StartedAtUtc,
            EndsAtUtc = subscription.EndsAtUtc,
            ProductFamilyCode = family.Code,
            ProductFamilyName = family.Name,
            PlanCode = plan.Code,
            PlanName = plan.Name,
            BillingPeriod = plan.BillingPeriod,
            Price = plan.Price,
            Currency = plan.Currency
        };
    }
}
