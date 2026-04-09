namespace OmnilensScraping.Persistence.Entities;

public class ProductFamily
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<Plan> Plans { get; set; } = new List<Plan>();
}

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductFamilyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string BillingPeriod { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ProductFamily ProductFamily { get; set; } = null!;
    public ICollection<PlanEntitlement> PlanEntitlements { get; set; } = new List<PlanEntitlement>();
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
}

public class Entitlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<PlanEntitlement> PlanEntitlements { get; set; } = new List<PlanEntitlement>();
}

public class PlanEntitlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public Guid EntitlementId { get; set; }
    public string? StringValue { get; set; }
    public decimal? NumericValue { get; set; }
    public bool? BooleanValue { get; set; }

    public Plan Plan { get; set; } = null!;
    public Entitlement Entitlement { get; set; } = null!;
}

public class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public string Status { get; set; } = "Active";
    public bool AutoRenew { get; set; } = true;
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndsAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser User { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
}
