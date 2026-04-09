namespace OmnilensScraping.Models;

public sealed class CreateReferralLinkRequest
{
    public Guid OfferId { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmCampaign { get; set; }
}

public sealed class ReferralLinkResponse
{
    public string Token { get; set; } = string.Empty;
    public string TrackedUrl { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid OfferId { get; set; }
    public Guid CanonicalProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string RetailerCode { get; set; } = string.Empty;
    public string RetailerName { get; set; } = string.Empty;
    public string OfferUrl { get; set; } = string.Empty;
}

public sealed class ReferralDashboardResponse
{
    public int TotalClicks { get; set; }
    public int TotalConversions { get; set; }
    public decimal TotalCommission { get; set; }
    public string? Currency { get; set; }
    public IReadOnlyCollection<ReferralSourceStatResponse> BySource { get; set; } = Array.Empty<ReferralSourceStatResponse>();
    public IReadOnlyCollection<ReferralRecentClickResponse> RecentClicks { get; set; } = Array.Empty<ReferralRecentClickResponse>();
}

public sealed class ReferralSourceStatResponse
{
    public Guid SourceId { get; set; }
    public string RetailerCode { get; set; } = string.Empty;
    public string RetailerName { get; set; } = string.Empty;
    public int Clicks { get; set; }
    public int Conversions { get; set; }
    public decimal Commission { get; set; }
}

public sealed class ReferralRecentClickResponse
{
    public Guid ClickId { get; set; }
    public DateTimeOffset ClickedAtUtc { get; set; }
    public Guid CanonicalProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string RetailerName { get; set; } = string.Empty;
    public string OfferUrl { get; set; } = string.Empty;
    public int ConversionCount { get; set; }
    public decimal CommissionAmount { get; set; }
    public string? Currency { get; set; }
}

public sealed class CreateConversionEventRequest
{
    public Guid? ClickEventId { get; set; }
    public Guid? SourceId { get; set; }
    public string? ExternalOrderRef { get; set; }
    public decimal? CommissionAmount { get; set; }
    public string? Currency { get; set; }
}

public sealed class ConversionEventResponse
{
    public Guid Id { get; set; }
    public Guid? ClickEventId { get; set; }
    public Guid SourceId { get; set; }
    public string? ExternalOrderRef { get; set; }
    public decimal? CommissionAmount { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset ConvertedAtUtc { get; set; }
}

