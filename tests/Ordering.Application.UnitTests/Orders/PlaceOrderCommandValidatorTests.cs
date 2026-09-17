using Ordering.Application.Orders.PlaceOrder;
using Ordering.Domain.Orders;

namespace Ordering.Application.UnitTests.Orders;

public sealed class PlaceOrderCommandValidatorTests
{
    private readonly PlaceOrderCommandValidator _validator = new();

    [Fact]
    public void Validate_NoItems_FailsOnItems()
    {
        var result = _validator.Validate(new PlaceOrderCommand("pos-001", "bar@example.com", []));

        result.Errors.ShouldContain(error => error.PropertyName == "Items");
    }

    [Fact]
    public void Validate_SameSkuWithDifferentCase_FailsOnItems()
    {
        var result = _validator.Validate(new PlaceOrderCommand(
            "pos-001",
            "bar@example.com",
            [new PlaceOrderItem("GOLDEN-LAGER-350", 1), new PlaceOrderItem("golden-lager-350", 2)]));

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Items");
    }

    [Fact]
    public void Validate_MoreThanMaxItems_FailsOnItems()
    {
        var items = Enumerable.Range(1, Order.MaxItems + 1).Select(index => new PlaceOrderItem($"SKU-{index}", 1)).ToList();

        var result = _validator.Validate(new PlaceOrderCommand("pos-001", "bar@example.com", items));

        result.Errors.ShouldContain(error => error.PropertyName == "Items");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void Validate_QuantityWithinRange_IsValid(int quantity)
    {
        var result = _validator.Validate(new PlaceOrderCommand(
            "pos-001",
            "bar@example.com",
            [new PlaceOrderItem("GOLDEN-LAGER-350", quantity)]));

        result.IsValid.ShouldBeTrue();
    }
}
