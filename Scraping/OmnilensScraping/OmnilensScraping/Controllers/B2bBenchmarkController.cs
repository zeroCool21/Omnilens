using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[Authorize(Roles = "B2B,ADMIN")]
[ApiController]
[Route("api/b2b/benchmark")]
public sealed class B2bBenchmarkController : ControllerBase
{
    private readonly OmnilensDbContext _dbContext;

    public B2bBenchmarkController(OmnilensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("current")]
    public async Task<ActionResult<B2bBenchmarkCurrentResponse>> GetCurrent(
        [FromQuery] string? q,
        [FromQuery] string? brand,
        [FromQuery] string? category,
        [FromQuery] string? vertical,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var rows = await BuildCurrentRowsAsync(q, brand, category, vertical, source, cancellationToken);
        var pagedRows = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Ok(new B2bBenchmarkCurrentResponse
        {
            Total = rows.Count,
            Items = pagedRows
        });
    }

    [HttpGet("trends")]
    public async Task<ActionResult<B2bBenchmarkTrendResponse>> GetTrends(
        [FromQuery] string? q,
        [FromQuery] string? brand,
        [FromQuery] string? category,
        [FromQuery] string? vertical,
        [FromQuery] string? source,
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);

        var products = await LoadProductsForBenchmarkAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var rows = ApplyProductFilters(products, q, brand, category, vertical, source)
            .SelectMany(product => product.SourceProducts.Select(sourceProduct => new { product, sourceProduct }))
            .Where(item => string.IsNullOrWhiteSpace(source) ||
                           item.sourceProduct.Source.RetailerCode.Equals(source.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(item =>
            {
                var samples = item.sourceProduct.PriceHistoryEntries
                    .Where(entry => entry.RecordedAtUtc >= cutoff && entry.Price.HasValue)
                    .OrderBy(entry => entry.RecordedAtUtc)
                    .ToArray();

                if (samples.Length == 0)
                {
                    return null;
                }

                var first = samples.First();
                var latest = samples.Last();
                var latestPrice = latest.Price;
                var firstPrice = first.Price;
                decimal? absoluteChange = latestPrice.HasValue && firstPrice.HasValue
                    ? latestPrice.Value - firstPrice.Value
                    : null;
                decimal? percentChange = absoluteChange.HasValue && firstPrice.HasValue && firstPrice.Value != 0m
                    ? Math.Round((absoluteChange.Value / firstPrice.Value) * 100m, 2)
                    : null;

                return new B2bBenchmarkTrendRow
                {
                    CanonicalProductId = item.product.Id,
                    Title = item.product.Title,
                    Brand = item.product.Brand,
                    RetailerCode = item.sourceProduct.Source.RetailerCode,
                    LatestPrice = latestPrice,
                    MinimumPrice = samples.Min(entry => entry.Price),
                    MaximumPrice = samples.Max(entry => entry.Price),
                    AbsoluteChange = absoluteChange,
                    PercentChange = percentChange,
                    Samples = samples.Length,
                    LatestRecordedAtUtc = latest.RecordedAtUtc
                };
            })
            .Where(item => item is not null)
            .Cast<B2bBenchmarkTrendRow>()
            .OrderBy(item => item.Brand)
            .ThenBy(item => item.Title)
            .ThenBy(item => item.RetailerCode)
            .ToArray();

        return Ok(new B2bBenchmarkTrendResponse
        {
            Days = days,
            Items = rows
        });
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCurrentCsv(
        [FromQuery] string? q,
        [FromQuery] string? brand,
        [FromQuery] string? category,
        [FromQuery] string? vertical,
        [FromQuery] string? source,
        CancellationToken cancellationToken = default)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasCsvExportAccessAsync(userId.Value, cancellationToken))
        {
            return Forbid();
        }

        var rows = await BuildCurrentRowsAsync(q, brand, category, vertical, source, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("CanonicalProductId,Title,Brand,Category,Vertical,BestPrice,WorstPrice,AveragePrice,PriceSpread,OfferCount,CompetitorCount,Offers");

        foreach (var row in rows)
        {
            var offers = string.Join(" | ", row.Offers.Select(offer =>
                $"{offer.RetailerCode}:{FormatDecimal(offer.Price)}:{offer.AvailabilityText}"));

            builder.AppendLine(string.Join(',',
                EscapeCsv(row.CanonicalProductId.ToString()),
                EscapeCsv(row.Title),
                EscapeCsv(row.Brand),
                EscapeCsv(row.Category),
                EscapeCsv(row.Vertical),
                EscapeCsv(FormatDecimal(row.BestPrice)),
                EscapeCsv(FormatDecimal(row.WorstPrice)),
                EscapeCsv(FormatDecimal(row.AveragePrice)),
                EscapeCsv(FormatDecimal(row.PriceSpread)),
                EscapeCsv(row.OfferCount.ToString(CultureInfo.InvariantCulture)),
                EscapeCsv(row.CompetitorCount.ToString(CultureInfo.InvariantCulture)),
                EscapeCsv(offers)));
        }

        var fileName = $"omnilens-b2b-benchmark-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv; charset=utf-8", fileName);
    }

    private async Task<List<B2bBenchmarkProductRow>> BuildCurrentRowsAsync(
        string? q,
        string? brand,
        string? category,
        string? vertical,
        string? source,
        CancellationToken cancellationToken)
    {
        var products = await LoadProductsForBenchmarkAsync(cancellationToken);

        return ApplyProductFilters(products, q, brand, category, vertical, source)
            .Select(product =>
            {
                var offers = product.SourceProducts
                    .Where(sourceProduct => string.IsNullOrWhiteSpace(source) ||
                                            sourceProduct.Source.RetailerCode.Equals(source.Trim(), StringComparison.OrdinalIgnoreCase))
                    .SelectMany(sourceProduct => sourceProduct.ProductOffers
                        .Where(offer => offer.IsLatest)
                        .Select(offer => new B2bBenchmarkOfferRow
                        {
                            RetailerCode = sourceProduct.Source.RetailerCode,
                            RetailerName = sourceProduct.Source.DisplayName,
                            Price = offer.Price,
                            Currency = offer.Currency,
                            AvailabilityText = offer.AvailabilityText,
                            ScrapedAtUtc = offer.ScrapedAtUtc
                        }))
                    .OrderBy(offer => offer.Price ?? decimal.MaxValue)
                    .ThenBy(offer => offer.RetailerCode)
                    .ToArray();

                if (offers.Length == 0)
                {
                    return null;
                }

                var priceValues = offers
                    .Where(offer => offer.Price.HasValue)
                    .Select(offer => offer.Price!.Value)
                    .ToArray();

                decimal? bestPrice = priceValues.Length > 0 ? priceValues.Min() : null;
                decimal? worstPrice = priceValues.Length > 0 ? priceValues.Max() : null;
                decimal? averagePrice = priceValues.Length > 0 ? Math.Round(priceValues.Average(), 2) : null;
                decimal? priceSpread = bestPrice.HasValue && worstPrice.HasValue
                    ? worstPrice.Value - bestPrice.Value
                    : null;

                return new B2bBenchmarkProductRow
                {
                    CanonicalProductId = product.Id,
                    Title = product.Title,
                    Brand = product.Brand,
                    Category = product.CategoryName,
                    Vertical = product.Vertical,
                    BestPrice = bestPrice,
                    WorstPrice = worstPrice,
                    AveragePrice = averagePrice,
                    PriceSpread = priceSpread,
                    OfferCount = offers.Length,
                    CompetitorCount = offers.Select(offer => offer.RetailerCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    Offers = offers
                };
            })
            .Where(item => item is not null)
            .Cast<B2bBenchmarkProductRow>()
            .OrderBy(item => item.Brand)
            .ThenBy(item => item.Title)
            .ThenBy(item => item.BestPrice ?? decimal.MaxValue)
            .ToList();
    }

    private async Task<List<CanonicalProduct>> LoadProductsForBenchmarkAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.CanonicalProducts
            .AsNoTracking()
            .Include(item => item.SourceProducts)
                .ThenInclude(item => item.Source)
            .Include(item => item.SourceProducts)
                .ThenInclude(item => item.ProductOffers)
            .Include(item => item.SourceProducts)
                .ThenInclude(item => item.PriceHistoryEntries)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> HasCsvExportAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("ADMIN"))
        {
            return true;
        }

        return await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(item => item.Plan)
                .ThenInclude(item => item.PlanEntitlements)
                    .ThenInclude(item => item.Entitlement)
            .Where(item => item.UserId == userId && item.Status == "Active" && item.Plan.Audience == "B2B")
            .AnyAsync(item => item.Plan.PlanEntitlements.Any(planEntitlement =>
                planEntitlement.Entitlement.Code == "CSV_EXPORT" &&
                planEntitlement.BooleanValue == true), cancellationToken);
    }

    private static IEnumerable<CanonicalProduct> ApplyProductFilters(
        IEnumerable<CanonicalProduct> products,
        string? q,
        string? brand,
        string? category,
        string? vertical,
        string? source)
    {
        var filtered = products;

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            filtered = filtered.Where(item =>
                item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (item.Brand?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (item.Gtin?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            filtered = filtered.Where(item => string.Equals(item.Brand, brand.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(item => string.Equals(item.CategoryName, category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(vertical))
        {
            filtered = filtered.Where(item => string.Equals(item.Vertical, vertical.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            filtered = filtered.Where(item => item.SourceProducts.Any(sourceProduct =>
                sourceProduct.Source.RetailerCode.Equals(source.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        return filtered;
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string FormatDecimal(decimal? value)
    {
        return value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
