namespace OmnilensScraping.Persistence.Entities;

public class WishlistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CanonicalProductId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser User { get; set; } = null!;
    public CanonicalProduct CanonicalProduct { get; set; } = null!;
}

public class AlertRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CanonicalProductId { get; set; }
    public decimal? TargetPrice { get; set; }
    public bool NotifyOnRestock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser User { get; set; } = null!;
    public CanonicalProduct CanonicalProduct { get; set; } = null!;
    public ICollection<AlertDelivery> AlertDeliveries { get; set; } = new List<AlertDelivery>();
}

public class AlertDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlertRuleId { get; set; }
    public string TriggerReason { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset DeliveredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AlertRule AlertRule { get; set; } = null!;
}

public class ClickEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public Guid CanonicalProductId { get; set; }
    public Guid SourceId { get; set; }
    public string OfferUrl { get; set; } = string.Empty;
    public string? UtmSource { get; set; }
    public string? UtmCampaign { get; set; }
    public DateTimeOffset ClickedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser? User { get; set; }
    public CanonicalProduct CanonicalProduct { get; set; } = null!;
    public Source Source { get; set; } = null!;
    public ICollection<ConversionEvent> ConversionEvents { get; set; } = new List<ConversionEvent>();
}

public class ConversionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ClickEventId { get; set; }
    public Guid SourceId { get; set; }
    public string? ExternalOrderRef { get; set; }
    public decimal? CommissionAmount { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset ConvertedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ClickEvent? ClickEvent { get; set; }
    public Source Source { get; set; } = null!;
}

public class SourceRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public string RunType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public int ItemsFound { get; set; }
    public int ItemsSaved { get; set; }
    public string? ErrorText { get; set; }

    public Source Source { get; set; } = null!;
    public ICollection<SourceRunLog> Logs { get; set; } = new List<SourceRunLog>();
}

public class SourceRunLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceRunId { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public SourceRun SourceRun { get; set; } = null!;
}
