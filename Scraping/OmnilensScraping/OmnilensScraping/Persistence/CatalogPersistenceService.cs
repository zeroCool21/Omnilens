using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence.Entities;
using OmnilensScraping.Scraping;

namespace OmnilensScraping.Persistence;

public sealed class CatalogPersistenceService
{
    private readonly OmnilensDbContext _dbContext;

    public CatalogPersistenceService(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CatalogPersistenceResult> PersistScrapeResultAsync(
        RetailerDefinition definition,
        ProductData product,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.Sources
            .SingleAsync(item => item.RetailerCode == definition.Retailer.ToString(), cancellationToken);

        var canonicalCreated = false;
        var sourceProductCreated = false;

        var canonicalProduct = await FindCanonicalProductAsync(product, definition, cancellationToken);
        if (canonicalProduct is null)
        {
            canonicalProduct = CreateCanonicalProduct(product, definition);
            _dbContext.CanonicalProducts.Add(canonicalProduct);
            canonicalCreated = true;
        }
        else
        {
            UpdateCanonicalProduct(canonicalProduct, product, definition);
        }

        var sourceProduct = await _dbContext.SourceProducts
            .Include(item => item.ProductOffers.Where(offer => offer.IsLatest))
            .Include(item => item.PriceHistoryEntries)
            .SingleOrDefaultAsync(
                item => item.SourceId == source.Id && item.SourceUrl == product.Url,
                cancellationToken);

        if (sourceProduct is null)
        {
            sourceProduct = new SourceProduct
            {
                SourceId = source.Id,
                SourceUrl = product.Url
            };

            _dbContext.SourceProducts.Add(sourceProduct);
            sourceProductCreated = true;
        }

        sourceProduct.CanonicalProduct = canonicalProduct;
        sourceProduct.SourceProductKey = product.Sku ?? product.Gtin;
        sourceProduct.Title = product.Title ?? canonicalProduct.Title;
        sourceProduct.Brand = product.Brand ?? canonicalProduct.Brand;
        sourceProduct.Sku = product.Sku;
        sourceProduct.Gtin = product.Gtin;
        sourceProduct.Currency = product.Currency;
        sourceProduct.AvailabilityText = product.Availability;
        sourceProduct.ImageUrl = product.ImageUrl;
        sourceProduct.Description = product.Description;
        sourceProduct.LastScrapedAtUtc = product.ScrapedAtUtc;
        sourceProduct.LastSuccessAtUtc = product.ScrapedAtUtc;
        sourceProduct.IsActive = true;

        foreach (var existingOffer in sourceProduct.ProductOffers.Where(item => item.IsLatest))
        {
            existingOffer.IsLatest = false;
        }

        var offer = new ProductOffer
        {
            SourceProduct = sourceProduct,
            Price = product.Price,
            PriceText = product.PriceText,
            Currency = product.Currency,
            AvailabilityText = product.Availability,
            StockStatus = NormalizeStockStatus(product.Availability),
            OfferUrl = product.Url,
            ScrapedAtUtc = product.ScrapedAtUtc,
            IsLatest = true
        };

        _dbContext.ProductOffers.Add(offer);

        Guid? priceHistoryId = null;
        var latestHistory = sourceProduct.PriceHistoryEntries
            .MaxBy(item => item.RecordedAtUtc);

        if (latestHistory is null ||
            latestHistory.Price != product.Price ||
            !string.Equals(latestHistory.AvailabilityText, product.Availability, StringComparison.OrdinalIgnoreCase))
        {
            var historyEntry = new PriceHistoryEntry
            {
                SourceProduct = sourceProduct,
                Price = product.Price,
                Currency = product.Currency,
                AvailabilityText = product.Availability,
                RecordedAtUtc = product.ScrapedAtUtc
            };

            _dbContext.PriceHistory.Add(historyEntry);
            priceHistoryId = historyEntry.Id;
        }

        await UpsertAttributesAsync(canonicalProduct, product, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogPersistenceResult
        {
            SourceId = source.Id,
            SourceProductId = sourceProduct.Id,
            CanonicalProductId = canonicalProduct.Id,
            ProductOfferId = offer.Id,
            PriceHistoryEntryId = priceHistoryId,
            CanonicalProductCreated = canonicalCreated,
            SourceProductCreated = sourceProductCreated
        };
    }

    private async Task<CanonicalProduct?> FindCanonicalProductAsync(
        ProductData product,
        RetailerDefinition definition,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(product.Gtin))
        {
            var byGtin = await _dbContext.CanonicalProducts
                .FirstOrDefaultAsync(item => item.Gtin == product.Gtin, cancellationToken);

            if (byGtin is not null)
            {
                return byGtin;
            }
        }

        var normalizedTitle = NormalizeText(product.Title);
        var normalizedBrand = NormalizeText(product.Brand);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return null;
        }

        return await _dbContext.CanonicalProducts
            .FirstOrDefaultAsync(
                item => item.Vertical == definition.Category.ToString() &&
                        item.Title.ToLower() == normalizedTitle &&
                        (item.Brand ?? string.Empty).ToLower() == normalizedBrand,
                cancellationToken);
    }

    private static CanonicalProduct CreateCanonicalProduct(ProductData product, RetailerDefinition definition)
    {
        var title = product.Title?.Trim();

        return new CanonicalProduct
        {
            Slug = BuildSlug(product, definition),
            Title = string.IsNullOrWhiteSpace(title) ? "Prodotto senza titolo" : title,
            Brand = NormalizeNullable(product.Brand),
            CategoryName = definition.Category.ToString(),
            Gtin = NormalizeNullable(product.Gtin),
            CanonicalSku = NormalizeNullable(product.Sku),
            ImageUrl = NormalizeNullable(product.ImageUrl),
            Description = NormalizeNullable(product.Description),
            Vertical = definition.Category.ToString(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static void UpdateCanonicalProduct(CanonicalProduct canonicalProduct, ProductData product, RetailerDefinition definition)
    {
        canonicalProduct.Title = string.IsNullOrWhiteSpace(canonicalProduct.Title)
            ? product.Title?.Trim() ?? canonicalProduct.Title
            : canonicalProduct.Title;

        canonicalProduct.Brand ??= NormalizeNullable(product.Brand);
        canonicalProduct.Gtin ??= NormalizeNullable(product.Gtin);
        canonicalProduct.CanonicalSku ??= NormalizeNullable(product.Sku);
        canonicalProduct.ImageUrl ??= NormalizeNullable(product.ImageUrl);
        canonicalProduct.Description ??= NormalizeNullable(product.Description);
        canonicalProduct.CategoryName = definition.Category.ToString();
        canonicalProduct.Vertical = definition.Category.ToString();
        canonicalProduct.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private async Task UpsertAttributesAsync(CanonicalProduct canonicalProduct, ProductData product, CancellationToken cancellationToken)
    {
        var existingAttributes = await _dbContext.CanonicalProductAttributes
            .Where(item => item.CanonicalProductId == canonicalProduct.Id)
            .ToListAsync(cancellationToken);

        _dbContext.CanonicalProductAttributes.RemoveRange(existingAttributes);

        foreach (var specification in product.Specifications)
        {
            if (string.IsNullOrWhiteSpace(specification.Key) || string.IsNullOrWhiteSpace(specification.Value))
            {
                continue;
            }

            _dbContext.CanonicalProductAttributes.Add(new CanonicalProductAttribute
            {
                CanonicalProduct = canonicalProduct,
                AttributeName = specification.Key.Trim(),
                AttributeValue = specification.Value.Trim()
            });
        }
    }

    private static string BuildSlug(ProductData product, RetailerDefinition definition)
    {
        var parts = new[]
        {
            definition.Category.ToString(),
            product.Brand,
            product.Title,
            product.Gtin,
            product.Sku
        };

        var normalized = string.Join('-',
            parts.Where(part => !string.IsNullOrWhiteSpace(part))
                .SelectMany(part => part!.ToLowerInvariant()
                    .Select(character => char.IsLetterOrDigit(character) ? character : '-'))
                .ToArray());

        var slug = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug)
            ? $"product-{Guid.NewGuid():N}"
            : slug.Length > 256
                ? slug[..256]
                : slug;
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeStockStatus(string? availability)
    {
        if (string.IsNullOrWhiteSpace(availability))
        {
            return null;
        }

        var text = availability.ToLowerInvariant();
        if (text.Contains("non disponibile") || text.Contains("esaurit") || text.Contains("out of stock"))
        {
            return "Unavailable";
        }

        if (text.Contains("disponibile") || text.Contains("in stock"))
        {
            return "Available";
        }

        return "Unknown";
    }
}
