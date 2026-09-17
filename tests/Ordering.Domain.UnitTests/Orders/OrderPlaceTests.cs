using NSubstitute;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders;
using Ordering.Domain.Orders.Events;
using Ordering.Domain.ValueObjects;
using static Ordering.Domain.UnitTests.Builders.OrderBuilder;

namespace Ordering.Domain.UnitTests.Orders;

public sealed class OrderPlaceTests
{
    [Fact]
    public void Place_ValidLines_CreatesPendingOrderWithPriceSnapshotAndTotals()
    {
        var result = AnOrder()
            .WithCustomer("pos-042", "bar@example.com")
            .WithLine("GOLDEN-LAGER-350", unitPrice: 12.00m, quantity: 3)
            .WithLine("AMBER-ALE-600", unitPrice: 27.60m, quantity: 2)
            .Place();

        result.IsSuccess.ShouldBeTrue();
        var order = result.Value;
        order.Id.ShouldNotBe(Guid.Empty);
        order.Status.ShouldBe(OrderStatus.Pending);
        order.CustomerId.ShouldBe("pos-042");
        order.CustomerEmail.ShouldBe("bar@example.com");
        order.PlacedOnUtc.ShouldBe(DefaultPlacedOnUtc);
        order.CompletedOnUtc.ShouldBeNull();
        order.Items.Count.ShouldBe(2);
        order.Items[0].LineTotal.ShouldBe(Usd(36.00m));
        order.Subtotal.ShouldBe(Usd(91.20m));
        order.Discount.ShouldBe(Usd(0m));
        order.Total.ShouldBe(Usd(91.20m));
    }

    [Fact]
    public void Place_ValidLines_RaisesOrderPlacedDomainEventWithItems()
    {
        var order = AnOrder()
            .WithLine("GOLDEN-LAGER-350", 12.00m, 3)
            .WithLine("AMBER-ALE-600", 27.60m, 2)
            .Place()
            .Value;

        var domainEvent = order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderPlacedDomainEvent>();
        domainEvent.OrderId.ShouldBe(order.Id);
        domainEvent.CustomerId.ShouldBe(order.CustomerId);
        domainEvent.OccurredOnUtc.ShouldBe(DefaultPlacedOnUtc);
        domainEvent.Items.ShouldBe(
        [
            new OrderPlacedDomainEventItem("GOLDEN-LAGER-350", 3),
            new OrderPlacedDomainEventItem("AMBER-ALE-600", 2),
        ]);
    }

    [Fact]
    public void Place_WithDiscountPolicy_AsksPolicyWithSubtotalAndTotalQuantityAndAppliesDiscount()
    {
        var policy = Substitute.For<IDiscountPolicy>();
        policy.CalculateDiscount(Arg.Any<Money>(), Arg.Any<int>()).Returns(Usd(5.00m));

        var order = AnOrder()
            .WithLine("GOLDEN-LAGER-350", 10.00m, 4)
            .WithLine("CRISP-PILSNER-269", 10.00m, 6)
            .WithDiscountPolicy(policy)
            .Place()
            .Value;

        policy.Received(1).CalculateDiscount(Usd(100.00m), 10);
        order.Discount.ShouldBe(Usd(5.00m));
        order.Total.ShouldBe(Usd(95.00m));
    }

    [Fact]
    public void Place_ReachingVolumeTier_AppliesVolumeDiscount()
    {
        var policy = new VolumeDiscountPolicy([new VolumeDiscountTier(MinimumQuantity: 10, Rate: 0.05m)]);

        var order = AnOrder()
            .WithLine("SUNNY-WHEAT-330", 31.20m, 10)
            .WithDiscountPolicy(policy)
            .Place()
            .Value;

        order.Subtotal.ShouldBe(Usd(312.00m));
        order.Discount.ShouldBe(Usd(15.60m));
        order.Total.ShouldBe(Usd(296.40m));
    }

    [Fact]
    public void Place_NoLines_ReturnsNoItemsError()
    {
        var result = Order.Place("pos-001", "bar@example.com", [], NoDiscountPolicy.Instance, DefaultPlacedOnUtc);

        result.Error.ShouldBe(OrderErrors.NoItems);
    }

    [Fact]
    public void Place_MoreThanMaxItems_ReturnsTooManyItemsError()
    {
        var lines = Enumerable.Range(1, Order.MaxItems + 1).Select(index => Line($"SKU-{index}", 1m, 1));

        var result = AnOrder().WithLines(lines).Place();

        result.Error.ShouldBe(OrderErrors.TooManyItems);
    }

    [Fact]
    public void Place_DuplicateSku_ReturnsDuplicateSkuError()
    {
        var result = AnOrder()
            .WithLine("GOLDEN-LAGER-350", 12.00m, 1)
            .WithLine("golden-lager-350", 12.00m, 2)
            .Place();

        result.Error.ShouldBe(OrderErrors.DuplicateSku("GOLDEN-LAGER-350"));
    }

    [Fact]
    public void Place_MixedCurrencies_ReturnsMixedCurrenciesError()
    {
        var result = AnOrder()
            .WithLine("GOLDEN-LAGER-350", 12.00m, 1, currency: "USD")
            .WithLine("AMBER-ALE-600", 27.60m, 1, currency: "EUR")
            .Place();

        result.Error.ShouldBe(OrderErrors.MixedCurrencies);
    }

    [Fact]
    public void Place_PolicyReturnsDiscountGreaterThanSubtotal_ThrowsInvalidOperationException()
    {
        var policy = Substitute.For<IDiscountPolicy>();
        policy.CalculateDiscount(Arg.Any<Money>(), Arg.Any<int>()).Returns(Usd(1000m));

        Should.Throw<InvalidOperationException>(() =>
            AnOrder().WithLine("GOLDEN-LAGER-350", 12.00m, 1).WithDiscountPolicy(policy).Place());
    }

    [Theory]
    [InlineData("", "bar@example.com")]
    [InlineData("pos-001", " ")]
    public void Place_MissingCustomerData_ThrowsArgumentException(string customerId, string customerEmail)
    {
        Should.Throw<ArgumentException>(() => AnOrder().WithCustomer(customerId, customerEmail).Place());
    }

    private static Money Usd(decimal amount) => Money.Create(amount, "USD").Value;
}
