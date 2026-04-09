namespace OmnilensScraping.Models;

public sealed class UpsertWishlistRequest
{
    public Guid CanonicalProductId { get; set; }
}

public sealed class WishlistItemResponse
{
    public Guid CanonicalProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? BestPrice { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset AddedAtUtc { get; set; }
}

public sealed class UpsertAlertRequest
{
    public Guid CanonicalProductId { get; set; }
    public decimal? TargetPrice { get; set; }
    public bool NotifyOnRestock { get; set; }
}

public sealed class AlertRuleResponse
{
    public Guid Id { get; set; }
    public Guid CanonicalProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public decimal? TargetPrice { get; set; }
    public bool NotifyOnRestock { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class UpdateAlertStatusRequest
{
    public bool IsActive { get; set; }
}

public sealed class AlertDeliveryResponse
{
    public Guid Id { get; set; }
    public Guid AlertRuleId { get; set; }
    public Guid CanonicalProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string TriggerReason { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset DeliveredAtUtc { get; set; }
}
