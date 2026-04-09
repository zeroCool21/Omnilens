namespace OmnilensScraping.Persistence.Entities;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserType { get; set; } = "B2C";
    public bool IsActive { get; set; } = true;
    public string? CountryCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CompanyMember> CompanyMemberships { get; set; } = new List<CompanyMember>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserRefreshToken> RefreshTokens { get; set; } = new List<UserRefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<WishlistEntry> WishlistEntries { get; set; } = new List<WishlistEntry>();
    public ICollection<AlertRule> AlertRules { get; set; } = new List<AlertRule>();
    public ICollection<ClickEvent> ClickEvents { get; set; } = new List<ClickEvent>();
    public ICollection<PharmacyReservation> PharmacyReservations { get; set; } = new List<PharmacyReservation>();
    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
    public UserProfileSettings? ProfileSettings { get; set; }
}

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? VatCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CompanyMember> Members { get; set; } = new List<CompanyMember>();
    public ICollection<CompanyInvite> Invites { get; set; } = new List<CompanyInvite>();
}

public class CompanyMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Company Company { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

public class CompanyInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public Guid? AcceptedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Company Company { get; set; } = null!;
    public AppUser? AcceptedByUser { get; set; }
}

public class AppRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class AppPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class UserRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser User { get; set; } = null!;
    public AppRole Role { get; set; } = null!;
}

public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppRole Role { get; set; } = null!;
    public AppPermission Permission { get; set; } = null!;
}

public class UserRefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
}

public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
}

public class UserProfileSettings
{
    public Guid UserId { get; set; }
    public bool NotificationEmailEnabled { get; set; } = true;
    public bool NotificationPushEnabled { get; set; }
    public bool PrivacyConsentAccepted { get; set; }
    public bool MarketingConsentAccepted { get; set; }
    public string LanguageCode { get; set; } = "it";
    public string CountryCode { get; set; } = "IT";
    public string? SectorCode { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser User { get; set; } = null!;
}
