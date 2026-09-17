namespace Ordering.Domain.Discounts;

/// <summary>Orders with at least <see cref="MinimumQuantity"/> packs get <see cref="Rate"/> off (0.05 = 5%).</summary>
public sealed record VolumeDiscountTier(int MinimumQuantity, decimal Rate);
