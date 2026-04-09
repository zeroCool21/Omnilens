namespace OmnilensScraping.Models;

public sealed class UserProfileResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "it";
    public string CountryCode { get; set; } = "IT";
    public string? SectorCode { get; set; }
    public bool NotificationEmailEnabled { get; set; }
    public bool NotificationPushEnabled { get; set; }
    public bool PrivacyConsentAccepted { get; set; }
    public bool MarketingConsentAccepted { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
}

public sealed class UpdateUserProfileRequest
{
    public string? DisplayName { get; set; }
    public string? LanguageCode { get; set; }
    public string? CountryCode { get; set; }
    public string? SectorCode { get; set; }
    public bool? NotificationEmailEnabled { get; set; }
    public bool? NotificationPushEnabled { get; set; }
    public bool? PrivacyConsentAccepted { get; set; }
    public bool? MarketingConsentAccepted { get; set; }
}
