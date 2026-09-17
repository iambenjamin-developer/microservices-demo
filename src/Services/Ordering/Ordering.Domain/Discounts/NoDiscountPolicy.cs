using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Discounts;

/// <summary>Null Object strategy: used when no promotion applies, so callers never check for null.</summary>
public sealed class NoDiscountPolicy : IDiscountPolicy
{
    public static readonly NoDiscountPolicy Instance = new();

    private NoDiscountPolicy()
    {
    }

    public Money CalculateDiscount(Money subtotal, int totalQuantity)
    {
        ArgumentNullException.ThrowIfNull(subtotal);
        return Money.Zero(subtotal.Currency);
    }
}
