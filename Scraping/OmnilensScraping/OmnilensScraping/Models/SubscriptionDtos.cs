namespace OmnilensScraping.Models;

public sealed class ProductFamilyResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyCollection<PlanResponse> Plans { get; set; } = Array.Empty<PlanResponse>();
}

public sealed class PlanResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string BillingPeriod { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Description { get; set; }
    public IReadOnlyCollection<PlanEntitlementResponse> Entitlements { get; set; } = Array.Empty<PlanEntitlementResponse>();
}

public sealed class PlanEntitlementResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string? StringValue { get; set; }
    public decimal? NumericValue { get; set; }
    public bool? BooleanValue { get; set; }
}

public sealed class UserSubscriptionResponse
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool AutoRenew { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public string ProductFamilyCode { get; set; } = string.Empty;
    public string ProductFamilyName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string BillingPeriod { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
}

public sealed class ActivateSubscriptionRequest
{
    public string PlanCode { get; set; } = string.Empty;
    public bool AutoRenew { get; set; } = true;
}
