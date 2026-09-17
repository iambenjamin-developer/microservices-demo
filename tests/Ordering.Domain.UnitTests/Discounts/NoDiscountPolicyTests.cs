using Ordering.Domain.Discounts;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.UnitTests.Discounts;

public sealed class NoDiscountPolicyTests
{
    [Fact]
    public void CalculateDiscount_AnyOrder_ReturnsZeroInSubtotalCurrency()
    {
        var subtotal = Money.Create(999.99m, "EUR").Value;

        var discount = NoDiscountPolicy.Instance.CalculateDiscount(subtotal, totalQuantity: 1000);

        discount.ShouldBe(Money.Zero("EUR"));
    }
}
