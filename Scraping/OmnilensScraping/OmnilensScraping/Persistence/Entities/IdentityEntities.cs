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
    public ICollection<WishlistEntry> WishlistEntries { get; set; } = new List<WishlistEntry>();
    public ICollection<AlertRule> AlertRules { get; set; } = new List<AlertRule>();
    public ICollection<ClickEvent> ClickEvents { get; set; } = new List<ClickEvent>();
    public ICollection<PharmacyReservation> PharmacyReservations { get; set; } = new List<PharmacyReservation>();
}

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? VatCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CompanyMember> Members { get; set; } = new List<CompanyMember>();
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
