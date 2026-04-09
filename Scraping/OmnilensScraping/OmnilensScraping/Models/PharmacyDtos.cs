namespace OmnilensScraping.Models;

public sealed class PharmacyProductSearchResponse
{
    public int Total { get; set; }
    public IReadOnlyCollection<PharmacyProductSearchItemResponse> Items { get; set; } = Array.Empty<PharmacyProductSearchItemResponse>();
}

public sealed class PharmacyProductSearchItemResponse
{
    public Guid CanonicalProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string CategoryClass { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string? DosageForm { get; set; }
    public string? PackageSize { get; set; }
    public bool RequiresPrescription { get; set; }
    public bool IsOtc { get; set; }
    public bool IsSop { get; set; }
    public decimal? BestPrice { get; set; }
    public string? Currency { get; set; }
    public int AvailableLocationCount { get; set; }
}

public sealed class PharmacyProductDetailResponse
{
    public Guid CanonicalProductId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Vertical { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? BestPrice { get; set; }
    public string? Currency { get; set; }
    public PharmacyProductFactResponse? PharmacyFact { get; set; }
    public IReadOnlyDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    public IReadOnlyCollection<PharmacyLocationAvailabilityResponse> AvailableLocations { get; set; } = Array.Empty<PharmacyLocationAvailabilityResponse>();
}

public sealed class PharmacyProductFactResponse
{
    public string CategoryClass { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string? DosageForm { get; set; }
    public string? StrengthText { get; set; }
    public string? PackageSize { get; set; }
    public bool RequiresPrescription { get; set; }
    public bool IsOtc { get; set; }
    public bool IsSop { get; set; }
    public string? Manufacturer { get; set; }
}

public sealed class PharmacyLocationAvailabilityResponse
{
    public Guid LocationId { get; set; }
    public Guid SourceId { get; set; }
    public string RetailerCode { get; set; } = string.Empty;
    public string RetailerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsAvailable { get; set; }
    public string? AvailabilityText { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset? LastUpdatedAtUtc { get; set; }
}

public sealed class CreatePharmacyReservationRequest
{
    public Guid CanonicalProductId { get; set; }
    public Guid LocationId { get; set; }
    public string ReservationType { get; set; } = "Pickup";
    public string? NreCode { get; set; }
}

public sealed class PharmacyReservationResponse
{
    public Guid Id { get; set; }
    public Guid CanonicalProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string RetailerName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string ReservationType { get; set; } = string.Empty;
    public string? NreCode { get; set; }
    public string? PickupCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CreatePharmacyReminderRequest
{
    public Guid CanonicalProductId { get; set; }
    public string ReminderType { get; set; } = "Refill";
    public int IntervalDays { get; set; } = 30;
    public string? Notes { get; set; }
    public DateTimeOffset? FirstReminderAtUtc { get; set; }
}

public sealed class PharmacyReminderResponse
{
    public Guid Id { get; set; }
    public Guid CanonicalProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string ReminderType { get; set; } = string.Empty;
    public int IntervalDays { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset NextReminderAtUtc { get; set; }
    public DateTimeOffset? LastTriggeredAtUtc { get; set; }
    public bool IsDue { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
