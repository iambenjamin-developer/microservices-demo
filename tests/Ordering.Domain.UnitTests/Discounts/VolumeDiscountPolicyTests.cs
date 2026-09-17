using Ordering.Domain.Discounts;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.UnitTests.Discounts;

public sealed class VolumeDiscountPolicyTests
{
    private static readonly VolumeDiscountPolicy _policy = new(
    [
        new VolumeDiscountTier(MinimumQuantity: 50, Rate: 0.10m),
        new VolumeDiscountTier(MinimumQuantity: 10, Rate: 0.05m),
    ]);

    [Fact]
    public void CalculateDiscount_BelowLowestTier_ReturnsZero()
    {
        var discount = _policy.CalculateDiscount(Usd(200m), totalQuantity: 9);

        discount.ShouldBe(Usd(0m));
    }

    [Theory]
    [InlineData(10, 10.00)]
    [InlineData(49, 10.00)]
    [InlineData(50, 20.00)]
    [InlineData(500, 20.00)]
    public void CalculateDiscount_TierReached_AppliesOnlyHighestTierRate(int totalQuantity, decimal expected)
    {
        var discount = _policy.CalculateDiscount(Usd(200m), totalQuantity);

        discount.ShouldBe(Usd(expected));
    }

    [Fact]
    public void CalculateDiscount_NoTiers_ReturnsZero()
    {
        var policy = new VolumeDiscountPolicy([]);

        policy.CalculateDiscount(Usd(200m), totalQuantity: 1000).ShouldBe(Usd(0m));
    }

    [Theory]
    [InlineData(0, 0.05)]
    [InlineData(10, 0)]
    [InlineData(10, -0.05)]
    [InlineData(10, 0.51)]
    public void Constructor_InvalidTier_ThrowsArgumentException(int minimumQuantity, decimal rate)
    {
        Should.Throw<ArgumentException>(() => new VolumeDiscountPolicy([new VolumeDiscountTier(minimumQuantity, rate)]));
    }

    [Fact]
    public void Constructor_DuplicateMinimumQuantity_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new VolumeDiscountPolicy(
        [
            new VolumeDiscountTier(10, 0.05m),
            new VolumeDiscountTier(10, 0.10m),
        ]));
    }

    [Fact]
    public void Tiers_UnorderedInput_AreSortedByMinimumQuantityDescending()
    {
        var policy = new VolumeDiscountPolicy(
        [
            new VolumeDiscountTier(10, 0.05m),
            new VolumeDiscountTier(100, 0.15m),
            new VolumeDiscountTier(50, 0.10m),
        ]);

        policy.Tiers.Select(tier => tier.MinimumQuantity).ShouldBe([100, 50, 10]);
    }

    private static Money Usd(decimal amount) => Money.Create(amount, "USD").Value;
}
