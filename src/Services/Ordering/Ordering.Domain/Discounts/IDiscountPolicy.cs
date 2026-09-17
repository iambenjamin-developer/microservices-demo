using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Discounts;

/// <summary>
/// Strategy for pricing rules: the <see cref="Orders.Order"/> aggregate asks the policy for a discount
/// without knowing which rule is in force, so new promotions do not change the aggregate.
/// </summary>
public interface IDiscountPolicy
{
    /// <param name="subtotal">Sum of all line totals.</param>
    /// <param name="totalQuantity">Total number of packs in the order.</param>
    /// <returns>A discount in the subtotal currency, never greater than the subtotal.</returns>
    Money CalculateDiscount(Money subtotal, int totalQuantity);
}
