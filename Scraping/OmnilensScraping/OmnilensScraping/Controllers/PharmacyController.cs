using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[ApiController]
[Route("api/pharmacy")]
public sealed class PharmacyController : ControllerBase
{
    private static readonly HashSet<string> AllowedReservationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pickup",
        "Prescription"
    };

    private static readonly HashSet<string> AllowedReminderTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Refill",
        "Therapy"
    };

    private readonly OmnilensDbContext _dbContext;

    public PharmacyController(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("products/search")]
    public async Task<ActionResult<PharmacyProductSearchResponse>> SearchProducts(
        [FromQuery] string? q,
        [FromQuery] string? activeIngredient,
        [FromQuery] string? categoryClass,
        [FromQuery] bool? requiresPrescription,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var products = await LoadPharmacyProductsAsync(cancellationToken);
        var filtered = ApplyProductFilters(products, q, activeIngredient, categoryClass, requiresPrescription);

        var items = filtered
            .Select(product =>
            {
                var fact = product.PharmacyProductFact;
                return new PharmacyProductSearchItemResponse
                {
                    CanonicalProductId = product.Id,
                    Title = product.Title,
                    Brand = product.Brand,
                    CategoryClass = fact?.CategoryClass ?? "OTC",
                    ActiveIngredient = fact?.ActiveIngredient,
                    DosageForm = fact?.DosageForm,
                    PackageSize = fact?.PackageSize,
                    RequiresPrescription = fact?.RequiresPrescription ?? false,
                    IsOtc = fact?.IsOtc ?? false,
                    IsSop = fact?.IsSop ?? false,
                    BestPrice = GetBestPrice(product),
                    Currency = GetCurrency(product),
                    AvailableLocationCount = CountAvailableLocations(product)
                };
            })
            .OrderBy(item => item.RequiresPrescription)
            .ThenBy(item => item.Title)
            .ToList();

        return Ok(new PharmacyProductSearchResponse
        {
            Total = items.Count,
            Items = items
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray()
        });
    }

    [AllowAnonymous]
    [HttpGet("products/{id:guid}")]
    public async Task<ActionResult<PharmacyProductDetailResponse>> GetProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _dbContext.CanonicalProducts
            .AsNoTracking()
            .Include(item => item.Attributes)
            .Include(item => item.PharmacyProductFact)
            .Include(item => item.SourceProducts)
                .ThenInclude(item => item.Source)
            .Include(item => item.SourceProducts)
                .ThenInclude(item => item.ProductOffers)
            .SingleOrDefaultAsync(item => item.Id == id && item.Vertical == RetailerCategory.Pharmacy.ToString(), cancellationToken);

        if (product is null)
        {
            return NotFound("Prodotto farmacia non trovato.");
        }

        var locations = await BuildLocationResponsesAsync(id, null, null, cancellationToken);

        return Ok(new PharmacyProductDetailResponse
        {
            CanonicalProductId = product.Id,
            Slug = product.Slug,
            Title = product.Title,
            Brand = product.Brand,
            Category = product.CategoryName,
            Vertical = product.Vertical,
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            BestPrice = GetBestPrice(product),
            Currency = GetCurrency(product),
            PharmacyFact = MapFact(product.PharmacyProductFact),
            Attributes = product.Attributes
                .OrderBy(item => item.AttributeName)
                .ToDictionary(item => item.AttributeName, item => item.AttributeValue),
            AvailableLocations = locations
        });
    }

    [AllowAnonymous]
    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyCollection<PharmacyLocationAvailabilityResponse>>> GetLocations(
        [FromQuery] Guid? canonicalProductId,
        [FromQuery] string? city,
        [FromQuery] Guid? sourceId,
        CancellationToken cancellationToken)
    {
        var locations = await BuildLocationResponsesAsync(canonicalProductId, city, sourceId, cancellationToken);
        return Ok(locations);
    }

    [Authorize]
    [HttpGet("reservations/me")]
    public async Task<ActionResult<IReadOnlyCollection<PharmacyReservationResponse>>> GetReservations(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var reservations = await _dbContext.PharmacyReservations
            .AsNoTracking()
            .Include(item => item.Source)
            .Include(item => item.CanonicalProduct)
            .Include(item => item.PharmacyLocation)
            .Where(item => item.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        return Ok(reservations
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(MapReservation)
            .ToArray());
    }

    [Authorize]
    [HttpPost("reservations")]
    public async Task<ActionResult<PharmacyReservationResponse>> CreateReservation(
        [FromBody] CreatePharmacyReservationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var reservationType = NormalizeReservationType(request.ReservationType);
        if (!AllowedReservationTypes.Contains(reservationType))
        {
            return BadRequest("Tipo prenotazione non valido.");
        }

        var location = await _dbContext.PharmacyLocations
            .Include(item => item.Source)
            .SingleOrDefaultAsync(item => item.Id == request.LocationId, cancellationToken);

        if (location is null)
        {
            return NotFound("Sede farmacia non trovata.");
        }

        var product = await _dbContext.CanonicalProducts
            .Include(item => item.PharmacyProductFact)
            .Include(item => item.SourceProducts)
                .ThenInclude(item => item.ProductOffers)
            .SingleOrDefaultAsync(item => item.Id == request.CanonicalProductId && item.Vertical == RetailerCategory.Pharmacy.ToString(), cancellationToken);

        if (product is null)
        {
            return NotFound("Prodotto farmacia non trovato.");
        }

        var latestOffer = product.SourceProducts
            .Where(item => item.SourceId == location.SourceId)
            .SelectMany(item => item.ProductOffers)
            .Where(item => item.IsLatest)
            .OrderBy(item => item.Price ?? decimal.MaxValue)
            .FirstOrDefault();

        if (latestOffer is null)
        {
            return Conflict("Il prodotto non risulta disponibile presso la farmacia selezionata.");
        }

        var requiresPrescription = product.PharmacyProductFact?.RequiresPrescription == true ||
                                   reservationType.Equals("Prescription", StringComparison.OrdinalIgnoreCase);
        var isImmediatelyAvailable = IsAvailableText(latestOffer.AvailabilityText);

        if (requiresPrescription && string.IsNullOrWhiteSpace(request.NreCode))
        {
            return BadRequest("NRE obbligatorio per questa prenotazione.");
        }

        var reservation = new PharmacyReservation
        {
            UserId = userId.Value,
            SourceId = location.SourceId,
            PharmacyLocationId = location.Id,
            CanonicalProductId = product.Id,
            ReservationType = reservationType,
            NreCode = string.IsNullOrWhiteSpace(request.NreCode) ? null : request.NreCode.Trim().ToUpperInvariant(),
            PickupCode = isImmediatelyAvailable ? $"PK-{Guid.NewGuid():N}"[..11].ToUpperInvariant() : null,
            Status = isImmediatelyAvailable ? "PendingPickup" : "PendingConfirmation",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.PharmacyReservations.Add(reservation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        reservation.Source = location.Source;
        reservation.PharmacyLocation = location;
        reservation.CanonicalProduct = product;

        return Ok(MapReservation(reservation));
    }

    [Authorize]
    [HttpGet("reminders/me")]
    public async Task<ActionResult<IReadOnlyCollection<PharmacyReminderResponse>>> GetReminders(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        var reminders = await _dbContext.PharmacyReminders
            .AsNoTracking()
            .Include(item => item.CanonicalProduct)
            .Where(item => item.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        return Ok(reminders
            .OrderBy(item => item.NextReminderAtUtc)
            .Select(item => MapReminder(item, item.CanonicalProduct.Title, now))
            .ToArray());
    }

    [Authorize]
    [HttpPost("reminders")]
    public async Task<ActionResult<PharmacyReminderResponse>> CreateReminder(
        [FromBody] CreatePharmacyReminderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var reminderType = NormalizeReminderType(request.ReminderType);
        if (!AllowedReminderTypes.Contains(reminderType))
        {
            return BadRequest("Tipo reminder non valido.");
        }

        var intervalDays = Math.Clamp(request.IntervalDays, 1, 365);
        var product = await _dbContext.CanonicalProducts
            .SingleOrDefaultAsync(item => item.Id == request.CanonicalProductId && item.Vertical == RetailerCategory.Pharmacy.ToString(), cancellationToken);

        if (product is null)
        {
            return NotFound("Prodotto farmacia non trovato.");
        }

        var reminder = new PharmacyReminder
        {
            UserId = userId.Value,
            CanonicalProductId = product.Id,
            ReminderType = reminderType,
            IntervalDays = intervalDays,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            NextReminderAtUtc = request.FirstReminderAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow.AddDays(intervalDays),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.PharmacyReminders.Add(reminder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapReminder(reminder, product.Title, DateTimeOffset.UtcNow));
    }

    [Authorize]
    [HttpDelete("reminders/{id:guid}")]
    public async Task<ActionResult> DeleteReminder(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var reminder = await _dbContext.PharmacyReminders
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId.Value, cancellationToken);

        if (reminder is null)
        {
            return NotFound();
        }

        _dbContext.PharmacyReminders.Remove(reminder);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<List<CanonicalProduct>> LoadPharmacyProductsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.CanonicalProducts
            .AsNoTracking()
            .Include(item => item.PharmacyProductFact)
            .Include(item => item.Attributes)
            .Include(item => item.SourceProducts)
                .ThenInclude(item => item.Source)
            .Include(item => item.SourceProducts)
                .ThenInclude(item => item.ProductOffers)
            .Where(item => item.Vertical == RetailerCategory.Pharmacy.ToString())
            .ToListAsync(cancellationToken);
    }

    private IEnumerable<CanonicalProduct> ApplyProductFilters(
        IEnumerable<CanonicalProduct> products,
        string? q,
        string? activeIngredient,
        string? categoryClass,
        bool? requiresPrescription)
    {
        var filtered = products;

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            filtered = filtered.Where(item =>
                item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (item.Brand?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (item.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(activeIngredient))
        {
            var search = activeIngredient.Trim();
            filtered = filtered.Where(item =>
                (item.PharmacyProductFact?.ActiveIngredient?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (item.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(categoryClass))
        {
            var value = categoryClass.Trim();
            filtered = filtered.Where(item =>
                string.Equals(item.PharmacyProductFact?.CategoryClass, value, StringComparison.OrdinalIgnoreCase));
        }

        if (requiresPrescription.HasValue)
        {
            filtered = filtered.Where(item => (item.PharmacyProductFact?.RequiresPrescription ?? false) == requiresPrescription.Value);
        }

        return filtered;
    }

    private async Task<IReadOnlyCollection<PharmacyLocationAvailabilityResponse>> BuildLocationResponsesAsync(
        Guid? canonicalProductId,
        string? city,
        Guid? sourceId,
        CancellationToken cancellationToken)
    {
        var locations = await _dbContext.PharmacyLocations
            .AsNoTracking()
            .Include(item => item.Source)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(city))
        {
            locations = locations
                .Where(item => item.City.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (sourceId.HasValue)
        {
            locations = locations
                .Where(item => item.SourceId == sourceId.Value)
                .ToList();
        }

        var offerBySourceId = new Dictionary<Guid, ProductOfferSnapshot>();

        if (canonicalProductId.HasValue)
        {
            var offers = await _dbContext.ProductOffers
                .AsNoTracking()
                .Where(item => item.IsLatest && item.SourceProduct.CanonicalProductId == canonicalProductId.Value)
                .Select(item => new
                {
                    item.SourceProduct.SourceId,
                    item.Price,
                    item.Currency,
                    item.AvailabilityText,
                    item.ScrapedAtUtc
                })
                .ToListAsync(cancellationToken);

            offerBySourceId = offers
                .GroupBy(item => item.SourceId)
                .ToDictionary(
                    item => item.Key,
                    item =>
                    {
                        var best = item
                            .OrderBy(entry => entry.Price ?? decimal.MaxValue)
                            .First();

                        return new ProductOfferSnapshot(
                            best.Price,
                            best.Currency,
                            best.AvailabilityText,
                            best.ScrapedAtUtc);
                    });
        }

        return locations
            .Select(location =>
            {
                offerBySourceId.TryGetValue(location.SourceId, out var offer);
                var isAvailable = offer is not null && IsAvailableText(offer.AvailabilityText);

                return new PharmacyLocationAvailabilityResponse
                {
                    LocationId = location.Id,
                    SourceId = location.SourceId,
                    RetailerCode = location.Source.RetailerCode,
                    RetailerName = location.Source.DisplayName,
                    Name = location.Name,
                    Address = location.Address,
                    City = location.City,
                    Province = location.Province,
                    PostalCode = location.PostalCode,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    IsAvailable = isAvailable,
                    AvailabilityText = offer?.AvailabilityText,
                    Price = offer?.Price,
                    Currency = offer?.Currency,
                    LastUpdatedAtUtc = offer?.ScrapedAtUtc
                };
            })
            .OrderBy(item => item.City)
            .ThenBy(item => item.Name)
            .ToArray();
    }

    private static PharmacyProductFactResponse? MapFact(PharmacyProductFact? fact)
    {
        if (fact is null)
        {
            return null;
        }

        return new PharmacyProductFactResponse
        {
            CategoryClass = fact.CategoryClass,
            ActiveIngredient = fact.ActiveIngredient,
            DosageForm = fact.DosageForm,
            StrengthText = fact.StrengthText,
            PackageSize = fact.PackageSize,
            RequiresPrescription = fact.RequiresPrescription,
            IsOtc = fact.IsOtc,
            IsSop = fact.IsSop,
            Manufacturer = fact.Manufacturer
        };
    }

    private static PharmacyReservationResponse MapReservation(PharmacyReservation reservation)
    {
        return new PharmacyReservationResponse
        {
            Id = reservation.Id,
            CanonicalProductId = reservation.CanonicalProductId,
            ProductTitle = reservation.CanonicalProduct.Title,
            SourceId = reservation.SourceId,
            RetailerName = reservation.Source.DisplayName,
            LocationId = reservation.PharmacyLocationId,
            LocationName = reservation.PharmacyLocation.Name,
            ReservationType = reservation.ReservationType,
            NreCode = reservation.NreCode,
            PickupCode = reservation.PickupCode,
            Status = reservation.Status,
            CreatedAtUtc = reservation.CreatedAtUtc
        };
    }

    private static PharmacyReminderResponse MapReminder(
        PharmacyReminder reminder,
        string productTitle,
        DateTimeOffset now)
    {
        return new PharmacyReminderResponse
        {
            Id = reminder.Id,
            CanonicalProductId = reminder.CanonicalProductId,
            ProductTitle = productTitle,
            ReminderType = reminder.ReminderType,
            IntervalDays = reminder.IntervalDays,
            Notes = reminder.Notes,
            NextReminderAtUtc = reminder.NextReminderAtUtc,
            LastTriggeredAtUtc = reminder.LastTriggeredAtUtc,
            IsDue = reminder.IsActive && reminder.NextReminderAtUtc <= now,
            IsActive = reminder.IsActive,
            CreatedAtUtc = reminder.CreatedAtUtc
        };
    }

    private static decimal? GetBestPrice(CanonicalProduct product)
    {
        return product.SourceProducts
            .SelectMany(item => item.ProductOffers)
            .Where(item => item.IsLatest && item.Price.HasValue)
            .Select(item => item.Price)
            .Min();
    }

    private static string? GetCurrency(CanonicalProduct product)
    {
        return product.SourceProducts
            .SelectMany(item => item.ProductOffers)
            .Where(item => item.IsLatest && !string.IsNullOrWhiteSpace(item.Currency))
            .Select(item => item.Currency)
            .FirstOrDefault();
    }

    private static int CountAvailableLocations(CanonicalProduct product)
    {
        return product.SourceProducts
            .Where(item => item.Source.Category == RetailerCategory.Pharmacy.ToString())
            .Count(item => item.ProductOffers.Any(offer => offer.IsLatest && IsAvailableText(offer.AvailabilityText)));
    }

    private static bool IsAvailableText(string? availabilityText)
    {
        if (string.IsNullOrWhiteSpace(availabilityText))
        {
            return false;
        }

        var text = availabilityText.Trim().ToLowerInvariant();
        return !text.Contains("non disponibile", StringComparison.Ordinal) &&
               !text.Contains("nondisponibile", StringComparison.Ordinal) &&
               !text.Contains("esaurit", StringComparison.Ordinal) &&
               !text.Contains("out of stock", StringComparison.Ordinal) &&
               !text.Contains("outofstock", StringComparison.Ordinal);
    }

    private static string NormalizeReservationType(string? value)
    {
        return string.Equals(value?.Trim(), "Prescription", StringComparison.OrdinalIgnoreCase)
            ? "Prescription"
            : "Pickup";
    }

    private static string NormalizeReminderType(string? value)
    {
        return string.Equals(value?.Trim(), "Therapy", StringComparison.OrdinalIgnoreCase)
            ? "Therapy"
            : "Refill";
    }

    private sealed record ProductOfferSnapshot(
        decimal? Price,
        string? Currency,
        string? AvailabilityText,
        DateTimeOffset ScrapedAtUtc);
}
