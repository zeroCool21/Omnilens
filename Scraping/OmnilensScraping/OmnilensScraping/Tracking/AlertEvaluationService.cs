using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Tracking;

public sealed class AlertEvaluationService
{
    private readonly OmnilensDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly AlertingOptions _options;
    private readonly ILogger<AlertEvaluationService> _logger;

    public AlertEvaluationService(
        OmnilensDbContext dbContext,
        IEmailSender emailSender,
        IOptions<AlertingOptions> options,
        ILogger<AlertEvaluationService> logger)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AlertEvaluationResult> EvaluateAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cooldownMinutes = Math.Max(0, _options.CooldownMinutes);
        var cooldownCutoff = now.AddMinutes(-cooldownMinutes);

        var rules = await _dbContext.AlertRules
            .AsNoTracking()
            .Where(rule => rule.IsActive && (rule.TargetPrice != null || rule.NotifyOnRestock))
            .Select(rule => new AlertRuleSnapshot
            {
                Id = rule.Id,
                UserId = rule.UserId,
                UserEmail = rule.User.Email,
                UserEmailEnabled = rule.User.ProfileSettings == null || rule.User.ProfileSettings.NotificationEmailEnabled,
                CanonicalProductId = rule.CanonicalProductId,
                ProductTitle = rule.CanonicalProduct.Title,
                TargetPrice = rule.TargetPrice,
                NotifyOnRestock = rule.NotifyOnRestock
            })
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return new AlertEvaluationResult
            {
                RulesEvaluated = 0,
                DeliveriesCreated = 0
            };
        }

        var canonicalProductIds = rules
            .Select(rule => rule.CanonicalProductId)
            .Distinct()
            .ToList();

        var offers = await _dbContext.ProductOffers
            .AsNoTracking()
            .Where(offer =>
                offer.IsLatest &&
                offer.SourceProduct.CanonicalProductId != null &&
                canonicalProductIds.Contains(offer.SourceProduct.CanonicalProductId.Value))
            .Select(offer => new OfferSnapshot
            {
                CanonicalProductId = offer.SourceProduct.CanonicalProductId!.Value,
                SourceId = offer.SourceProduct.SourceId,
                RetailerCode = offer.SourceProduct.Source.RetailerCode,
                RetailerName = offer.SourceProduct.Source.DisplayName,
                Price = offer.Price,
                Currency = offer.Currency,
                AvailabilityText = offer.AvailabilityText,
                StockStatus = offer.StockStatus,
                OfferUrl = offer.OfferUrl
            })
            .ToListAsync(cancellationToken);

        var offersByProduct = offers
            .GroupBy(offer => offer.CanonicalProductId)
            .ToDictionary(group => group.Key, group => group.ToList());

        // SQLite provider: DateTimeOffset comparisons are not reliably translated, filter in-memory.
        var ruleIds = rules.Select(rule => rule.Id).Distinct().ToList();
        var recentCandidates = await _dbContext.AlertDeliveries
            .AsNoTracking()
            .Where(delivery => ruleIds.Contains(delivery.AlertRuleId))
            .Select(delivery => new
            {
                delivery.AlertRuleId,
                delivery.TriggerReason,
                delivery.DeliveredAtUtc
            })
            .ToListAsync(cancellationToken);

        var recentDeliveries = new HashSet<RecentDeliveryKey>(recentCandidates
            .Where(delivery => delivery.DeliveredAtUtc >= cooldownCutoff)
            .Select(delivery => new RecentDeliveryKey { AlertRuleId = delivery.AlertRuleId, TriggerReason = delivery.TriggerReason }));

        var deliveriesCreated = 0;
        var maxDeliveries = Math.Clamp(_options.MaxDeliveriesPerRun, 0, 10_000);

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (deliveriesCreated >= maxDeliveries)
            {
                _logger.LogWarning("Limite max deliveries per run raggiunto: {MaxDeliveriesPerRun}", maxDeliveries);
                break;
            }

            if (!offersByProduct.TryGetValue(rule.CanonicalProductId, out var productOffers) ||
                productOffers.Count == 0)
            {
                continue;
            }

            var bestPriceOffer = productOffers
                .Where(offer => offer.Price.HasValue)
                .OrderBy(offer => offer.Price)
                .FirstOrDefault();

            var bestAvailableOffer = productOffers
                .Where(offer => string.Equals(offer.StockStatus, "Available", StringComparison.OrdinalIgnoreCase))
                .OrderBy(offer => offer.Price ?? decimal.MaxValue)
                .FirstOrDefault();

            var didDeliver = false;

            if (rule.TargetPrice.HasValue &&
                bestPriceOffer?.Price is not null &&
                bestPriceOffer.Price.Value <= rule.TargetPrice.Value &&
                !recentDeliveries.Contains(new RecentDeliveryKey { AlertRuleId = rule.Id, TriggerReason = "TargetPrice" }))
            {
                var payload = JsonSerializer.Serialize(new
                {
                    rule.CanonicalProductId,
                    rule.ProductTitle,
                    rule.TargetPrice,
                    BestPrice = bestPriceOffer.Price,
                    bestPriceOffer.Currency,
                    bestPriceOffer.RetailerCode,
                    bestPriceOffer.RetailerName,
                    bestPriceOffer.OfferUrl,
                    bestPriceOffer.AvailabilityText
                });

                _dbContext.AlertDeliveries.Add(new AlertDelivery
                {
                    AlertRuleId = rule.Id,
                    TriggerReason = "TargetPrice",
                    PayloadJson = payload,
                    DeliveredAtUtc = now
                });

                recentDeliveries.Add(new RecentDeliveryKey { AlertRuleId = rule.Id, TriggerReason = "TargetPrice" });
                deliveriesCreated++;
                didDeliver = true;

                if (rule.UserEmailEnabled)
                {
                    await TrySendEmailAsync(
                        rule.UserEmail,
                        $"[OmniLens+] Alert prezzo: {rule.ProductTitle}",
                        $"Target: {rule.TargetPrice:0.00}\nBest: {bestPriceOffer.Price:0.00} {bestPriceOffer.Currency}\nStore: {bestPriceOffer.RetailerName}\nUrl: {bestPriceOffer.OfferUrl}",
                        cancellationToken);
                }
            }

            if (deliveriesCreated >= maxDeliveries)
            {
                break;
            }

            if (rule.NotifyOnRestock &&
                bestAvailableOffer is not null &&
                !recentDeliveries.Contains(new RecentDeliveryKey { AlertRuleId = rule.Id, TriggerReason = "Restock" }))
            {
                var payload = JsonSerializer.Serialize(new
                {
                    rule.CanonicalProductId,
                    rule.ProductTitle,
                    bestAvailableOffer.Currency,
                    bestAvailableOffer.RetailerCode,
                    bestAvailableOffer.RetailerName,
                    bestAvailableOffer.Price,
                    bestAvailableOffer.OfferUrl,
                    bestAvailableOffer.AvailabilityText
                });

                _dbContext.AlertDeliveries.Add(new AlertDelivery
                {
                    AlertRuleId = rule.Id,
                    TriggerReason = "Restock",
                    PayloadJson = payload,
                    DeliveredAtUtc = now
                });

                recentDeliveries.Add(new RecentDeliveryKey { AlertRuleId = rule.Id, TriggerReason = "Restock" });
                deliveriesCreated++;
                didDeliver = true;

                if (rule.UserEmailEnabled)
                {
                    await TrySendEmailAsync(
                        rule.UserEmail,
                        $"[OmniLens+] Restock: {rule.ProductTitle}",
                        $"Disponibile su {bestAvailableOffer.RetailerName}\nPrezzo: {(bestAvailableOffer.Price.HasValue ? $"{bestAvailableOffer.Price:0.00} {bestAvailableOffer.Currency}" : "n/d")}\nUrl: {bestAvailableOffer.OfferUrl}",
                        cancellationToken);
                }
            }

            if (!didDeliver)
            {
                continue;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AlertEvaluationResult
        {
            RulesEvaluated = rules.Count,
            DeliveriesCreated = deliveriesCreated
        };
    }

    private async Task TrySendEmailAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        try
        {
            await _emailSender.SendAsync(email.Trim(), subject, body, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Invio email fallito per {Email}", email);
        }
    }

    private sealed class AlertRuleSnapshot
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public bool UserEmailEnabled { get; set; }
        public Guid CanonicalProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public decimal? TargetPrice { get; set; }
        public bool NotifyOnRestock { get; set; }
    }

    private sealed class OfferSnapshot
    {
        public Guid CanonicalProductId { get; set; }
        public Guid SourceId { get; set; }
        public string RetailerCode { get; set; } = string.Empty;
        public string RetailerName { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string? Currency { get; set; }
        public string? AvailabilityText { get; set; }
        public string? StockStatus { get; set; }
        public string OfferUrl { get; set; } = string.Empty;
    }

    private sealed class RecentDeliveryKey : IEquatable<RecentDeliveryKey>
    {
        public Guid AlertRuleId { get; init; }
        public string TriggerReason { get; init; } = string.Empty;

        public bool Equals(RecentDeliveryKey? other)
        {
            if (other is null)
            {
                return false;
            }

            return AlertRuleId == other.AlertRuleId &&
                   string.Equals(TriggerReason, other.TriggerReason, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is RecentDeliveryKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(AlertRuleId, TriggerReason);
    }
}
