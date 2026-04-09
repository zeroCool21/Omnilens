using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[Authorize]
[ApiController]
[Route("api/wishlist")]
public sealed class WishlistController : ControllerBase
{
    private readonly OmnilensDbContext _dbContext;

    public WishlistController(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<WishlistItemResponse>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var items = await _dbContext.WishlistEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId.Value)
            .Include(item => item.CanonicalProduct)
                .ThenInclude(product => product.SourceProducts)
                    .ThenInclude(sourceProduct => sourceProduct.ProductOffers)
            .ToListAsync(cancellationToken);

        return Ok(items
            .Select(item => new WishlistItemResponse
            {
                CanonicalProductId = item.CanonicalProductId,
                Title = item.CanonicalProduct.Title,
                Brand = item.CanonicalProduct.Brand,
                ImageUrl = item.CanonicalProduct.ImageUrl,
                BestPrice = item.CanonicalProduct.SourceProducts
                    .SelectMany(product => product.ProductOffers)
                    .Where(offer => offer.IsLatest && offer.Price.HasValue)
                    .Select(offer => offer.Price)
                    .Min(),
                Currency = item.CanonicalProduct.SourceProducts
                    .SelectMany(product => product.ProductOffers)
                    .Where(offer => offer.IsLatest && !string.IsNullOrWhiteSpace(offer.Currency))
                    .Select(offer => offer.Currency)
                    .FirstOrDefault(),
                AddedAtUtc = item.CreatedAtUtc
            })
            .OrderByDescending(item => item.AddedAtUtc)
            .ToList());
    }

    [HttpPost]
    public async Task<ActionResult> Add([FromBody] UpsertWishlistRequest request, CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var productExists = await _dbContext.CanonicalProducts.AnyAsync(item => item.Id == request.CanonicalProductId, cancellationToken);
        if (!productExists)
        {
            return NotFound("Prodotto non trovato.");
        }

        var exists = await _dbContext.WishlistEntries
            .AnyAsync(item => item.UserId == userId.Value && item.CanonicalProductId == request.CanonicalProductId, cancellationToken);

        if (!exists)
        {
            _dbContext.WishlistEntries.Add(new WishlistEntry
            {
                UserId = userId.Value,
                CanonicalProductId = request.CanonicalProductId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpDelete("{canonicalProductId:guid}")]
    public async Task<ActionResult> Remove(Guid canonicalProductId, CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var item = await _dbContext.WishlistEntries
            .SingleOrDefaultAsync(
                entry => entry.UserId == userId.Value && entry.CanonicalProductId == canonicalProductId,
                cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        _dbContext.WishlistEntries.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
