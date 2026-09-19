using Ordering.Application.Orders;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.UnitTests.Mapping;

// Unmapped members are compile errors (Mapperly + warnings as errors); these tests check the values.
// The SQL translation of the projections is covered by Ordering.IntegrationTests.
public sealed class OrderMapperTests
{
    [Fact]
    public void ToResponse_PlacedOrder_FlattensMoneyAndItems()
    {
        var order = PlacedOrderWithDiscount();

        var response = order.ToResponse();

        response.Id.ShouldBe(order.Id);
        response.CustomerId.ShouldBe("pos-001");
        response.Status.ShouldBe(OrderStatus.Pending);
        response.Subtotal.ShouldBe(91.20m);
        response.Discount.ShouldBe(9.12m);
        response.Total.ShouldBe(82.08m);
        response.Currency.ShouldBe("USD");
        response.PlacedOnUtc.ShouldBe(order.PlacedOnUtc);
        response.CompletedOnUtc.ShouldBeNull();
        response.Items.ShouldBe(
        [
            new OrderItemResponse("AMBER-ALE-600", "Product AMBER-ALE-600", 27.60m, 2, 55.20m),
            new OrderItemResponse("GOLDEN-LAGER-350", "Product GOLDEN-LAGER-350", 12.00m, 3, 36.00m),
        ]);
    }

    [Fact]
    public void ToSummary_PlacedOrder_CountsItems()
    {
        var order = Order.Place(
            "pos-001",
            "bar@example.com",
            [Line("GOLDEN-LAGER-350", 12.00m, 1), Line("AMBER-ALE-600", 27.60m, 1)],
            NoDiscountPolicy.Instance,
            DateTimeOffset.UnixEpoch).Value;

        var summary = order.ToSummary();

        summary.ShouldBe(new OrderSummaryResponse(order.Id, OrderStatus.Pending, 2, 39.60m, "USD", DateTimeOffset.UnixEpoch, null));
    }

    [Fact]
    public void ProjectToResponse_PlacedOrder_MatchesInMemoryMapping()
    {
        var order = PlacedOrderWithDiscount();

        var projected = new[] { order }.AsQueryable().ProjectToResponse().Single();

        var expected = order.ToResponse();
        projected.ShouldBe(expected with { Items = projected.Items });
        projected.Items.ShouldBe(expected.Items);
    }

    [Fact]
    public void ProjectToSummary_PlacedOrder_MatchesInMemoryMapping()
    {
        var order = PlacedOrderWithDiscount();

        var projected = new[] { order }.AsQueryable().ProjectToSummary().Single();

        projected.ShouldBe(order.ToSummary());
    }

    private static Order PlacedOrderWithDiscount() =>
        Order.Place(
            "pos-001",
            "bar@example.com",
            [
                Line("GOLDEN-LAGER-350", 12.00m, 3),
                Line("AMBER-ALE-600", 27.60m, 2),
            ],
            new VolumeDiscountPolicy([new VolumeDiscountTier(MinimumQuantity: 5, Rate: 0.10m)]),
            new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero)).Value;

    private static OrderLine Line(string sku, decimal unitPrice, int quantity) =>
        new(Sku.Create(sku).Value, $"Product {sku}", Money.Create(unitPrice, "USD").Value, Quantity.Create(quantity).Value);
}
