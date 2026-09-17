using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Discounts;

/// <summary>
/// B2B volume pricing: the more packs an order contains, the bigger the discount.
/// Only the highest tier reached applies (tiers do not stack).
/// </summary>
public sealed class VolumeDiscountPolicy : IDiscountPolicy
{
    public const decimal MaxRate = 0.5m;

    private readonly VolumeDiscountTier[] _tiers;

    public VolumeDiscountPolicy(IEnumerable<VolumeDiscountTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        _tiers = [.. tiers.OrderByDescending(tier => tier.MinimumQuantity)];

        foreach (var tier in _tiers)
        {
            if (tier.MinimumQuantity < 1)
            {
                throw new ArgumentException($"Tier minimum quantity must be at least 1, but was {tier.MinimumQuantity}.", nameof(tiers));
            }

            if (tier.Rate is <= 0 or > MaxRate)
            {
                throw new ArgumentException($"Tier rate must be greater than 0 and at most {MaxRate}, but was {tier.Rate}.", nameof(tiers));
            }
        }

        if (_tiers.DistinctBy(tier => tier.MinimumQuantity).Count() != _tiers.Length)
        {
            throw new ArgumentException("Tier minimum quantities must be unique.", nameof(tiers));
        }
    }

    public IReadOnlyList<VolumeDiscountTier> Tiers => _tiers;

    public Money CalculateDiscount(Money subtotal, int totalQuantity)
    {
        ArgumentNullException.ThrowIfNull(subtotal);

        var tier = _tiers.FirstOrDefault(tier => totalQuantity >= tier.MinimumQuantity);

        return tier is null
            ? Money.Zero(subtotal.Currency)
            : subtotal.ApplyRate(tier.Rate);
    }
}
