using Ordering.Domain.Discounts;

namespace Ordering.Application.Pricing;

/// <summary>Options pattern for pricing rules (configuration section <c>Pricing</c>).</summary>
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    /// <summary>ISO 4217 code applied to Catalog prices, which carry no currency.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Volume discount tiers. Empty means no discount (<see cref="NoDiscountPolicy"/>).</summary>
    public List<VolumeDiscountTier> VolumeDiscountTiers { get; set; } = [];
}
