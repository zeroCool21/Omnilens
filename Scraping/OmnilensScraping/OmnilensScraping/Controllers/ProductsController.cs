using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly OmnilensDbContext _dbContext;

    public ProductsController(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<ActionResult<ProductSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] string? brand,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? availability,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = "title",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var userId = User.TryGetUserId();
        var query = _dbContext.CanonicalProducts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLowerInvariant();
            query = query.Where(item =>
                item.Title.ToLower().Contains(search) ||
                (item.Brand != null && item.Brand.ToLower().Contains(search)) ||
                (item.Gtin != null && item.Gtin.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryFilter = category.Trim().ToLowerInvariant();
            query = query.Where(item => item.CategoryName.ToLower() == categoryFilter || item.Vertical.ToLower() == categoryFilter);
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            var brandFilter = brand.Trim().ToLowerInvariant();
            query = query.Where(item => item.Brand != null && item.Brand.ToLower() == brandFilter);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            var sourceFilter = source.Trim().ToLowerInvariant();
            query = query.Where(item => item.SourceProducts.Any(product => product.Source.RetailerCode.ToLower() == sourceFilter));
        }

        if (!string.IsNullOrWhiteSpace(availability))
        {
            var availabilityFilter = availability.Trim().ToLowerInvariant();
            query = query.Where(item => item.SourceProducts
                .SelectMany(product => product.ProductOffers)
                .Any(offer => offer.IsLatest &&
                              offer.AvailabilityText != null &&
                              offer.AvailabilityText.ToLower().Contains(availabilityFilter)));
        }

        var products = await query
            .Include(item => item.SourceProducts)
                .ThenInclude(product => product.ProductOffers)
            .Include(item => item.WishlistEntries)
            .ToListAsync(cancellationToken);

        var projected = products.Select(item => new
        {
            Product = item,
            BestPrice = GetBestPrice(item),
            Currency = GetDisplayCurrency(item),
            SourceCount = item.SourceProducts
                .Select(product => product.SourceId)
                .Distinct()
                .Count(),
            HasAvailability = string.IsNullOrWhiteSpace(availability) || HasAvailability(item, availability),
            IsWishlisted = userId.HasValue && item.WishlistEntries.Any(entry => entry.UserId == userId.Value)
        });

        if (minPrice.HasValue)
        {
            projected = projected.Where(item => item.BestPrice.HasValue && item.BestPrice.Value >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            projected = projected.Where(item => item.BestPrice.HasValue && item.BestPrice.Value <= maxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(availability))
        {
            projected = projected.Where(item => item.HasAvailability);
        }

        projected = sort?.ToLowerInvariant() switch
        {
            "price" => projected
                .OrderBy(item => item.BestPrice ?? decimal.MaxValue)
                .ThenBy(item => item.Product.Title),
            "brand" => projected
                .OrderBy(item => item.Product.Brand)
                .ThenBy(item => item.Product.Title),
            _ => projected.OrderBy(item => item.Product.Title)
        };

        var total = projected.Count();

        var items = projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ProductSearchItemResponse
            {
                Id = item.Product.Id,
                Title = item.Product.Title,
                Brand = item.Product.Brand,
                Category = item.Product.CategoryName,
                Vertical = item.Product.Vertical,
                ImageUrl = item.Product.ImageUrl,
                BestPrice = item.BestPrice,
                Currency = item.Currency,
                SourceCount = item.SourceCount,
                IsWishlisted = item.IsWishlisted
            })
            .ToList();

        return Ok(new ProductSearchResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();

        var product = await _dbContext.CanonicalProducts
            .AsNoTracking()
            .Include(item => item.SourceProducts)
                .ThenInclude(product => product.ProductOffers)
            .Include(item => item.WishlistEntries)
            .Include(item => item.Attributes)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        return Ok(new ProductDetailResponse
        {
            Id = product.Id,
            Slug = product.Slug,
            Title = product.Title,
            Brand = product.Brand,
            Category = product.CategoryName,
            Vertical = product.Vertical,
            Gtin = product.Gtin,
            CanonicalSku = product.CanonicalSku,
            ImageUrl = product.ImageUrl,
            Description = product.Description,
            BestPrice = GetBestPrice(product),
            Currency = GetDisplayCurrency(product),
            IsWishlisted = userId.HasValue && product.WishlistEntries.Any(entry => entry.UserId == userId.Value),
            Attributes = product.Attributes
                .OrderBy(attribute => attribute.AttributeName)
                .ToDictionary(attribute => attribute.AttributeName, attribute => attribute.AttributeValue)
        });
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/offers")]
    public async Task<ActionResult<IReadOnlyCollection<ProductOfferResponse>>> GetOffers(Guid id, CancellationToken cancellationToken)
    {
        var offers = await _dbContext.ProductOffers
            .AsNoTracking()
            .Where(offer => offer.IsLatest && offer.SourceProduct.CanonicalProductId == id)
            .Select(offer => new ProductOfferResponse
            {
                OfferId = offer.Id,
                SourceId = offer.SourceProduct.SourceId,
                RetailerCode = offer.SourceProduct.Source.RetailerCode,
                RetailerName = offer.SourceProduct.Source.DisplayName,
                Price = offer.Price,
                PriceText = offer.PriceText,
                Currency = offer.Currency,
                AvailabilityText = offer.AvailabilityText,
                StockStatus = offer.StockStatus,
                OfferUrl = offer.OfferUrl,
                ScrapedAtUtc = offer.ScrapedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(offers.OrderBy(offer => offer.Price ?? decimal.MaxValue).ToList());
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/price-history")]
    public async Task<ActionResult<IReadOnlyCollection<ProductPriceHistoryPointResponse>>> GetPriceHistory(
        Guid id,
        [FromQuery] string? source,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PriceHistory
            .AsNoTracking()
            .Where(item => item.SourceProduct.CanonicalProductId == id);

        if (!string.IsNullOrWhiteSpace(source))
        {
            var sourceFilter = source.Trim().ToLowerInvariant();
            query = query.Where(item => item.SourceProduct.Source.RetailerCode.ToLower() == sourceFilter);
        }

        var points = await query
            .Select(item => new ProductPriceHistoryPointResponse
            {
                SourceProductId = item.SourceProductId,
                RetailerCode = item.SourceProduct.Source.RetailerCode,
                Price = item.Price,
                Currency = item.Currency,
                AvailabilityText = item.AvailabilityText,
                RecordedAtUtc = item.RecordedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(points
            .OrderByDescending(item => item.RecordedAtUtc)
            .Take(500)
            .ToList());
    }

    private static decimal? GetBestPrice(CanonicalProduct product)
    {
        return product.SourceProducts
            .SelectMany(sourceProduct => sourceProduct.ProductOffers)
            .Where(offer => offer.IsLatest && offer.Price.HasValue)
            .Select(offer => offer.Price)
            .Min();
    }

    private static string? GetDisplayCurrency(CanonicalProduct product)
    {
        return product.SourceProducts
            .SelectMany(sourceProduct => sourceProduct.ProductOffers)
            .Where(offer => offer.IsLatest && !string.IsNullOrWhiteSpace(offer.Currency))
            .Select(offer => offer.Currency)
            .FirstOrDefault();
    }

    private static bool HasAvailability(CanonicalProduct product, string availability)
    {
        var filter = availability.Trim();
        return product.SourceProducts
            .SelectMany(sourceProduct => sourceProduct.ProductOffers)
            .Any(offer =>
                offer.IsLatest &&
                offer.AvailabilityText != null &&
                offer.AvailabilityText.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}
