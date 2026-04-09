namespace OmnilensScraping.Persistence.Entities;

public class PharmacyProductFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CanonicalProductId { get; set; }
    public string CategoryClass { get; set; } = "OTC";
    public string? ActiveIngredient { get; set; }
    public string? DosageForm { get; set; }
    public string? StrengthText { get; set; }
    public string? PackageSize { get; set; }
    public bool RequiresPrescription { get; set; }
    public bool IsOtc { get; set; }
    public bool IsSop { get; set; }
    public string? Manufacturer { get; set; }

    public CanonicalProduct CanonicalProduct { get; set; } = null!;
}

public class PharmacyLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? OpeningHoursJson { get; set; }

    public Source Source { get; set; } = null!;
    public ICollection<PharmacyReservation> Reservations { get; set; } = new List<PharmacyReservation>();
}

public class PharmacyReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SourceId { get; set; }
    public Guid PharmacyLocationId { get; set; }
    public Guid CanonicalProductId { get; set; }
    public string ReservationType { get; set; } = string.Empty;
    public string? NreCode { get; set; }
    public string? PickupCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser User { get; set; } = null!;
    public Source Source { get; set; } = null!;
    public PharmacyLocation PharmacyLocation { get; set; } = null!;
    public CanonicalProduct CanonicalProduct { get; set; } = null!;
}

public class PharmacyReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CanonicalProductId { get; set; }
    public string ReminderType { get; set; } = string.Empty;
    public int IntervalDays { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset NextReminderAtUtc { get; set; }
    public DateTimeOffset? LastTriggeredAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser User { get; set; } = null!;
    public CanonicalProduct CanonicalProduct { get; set; } = null!;
}
