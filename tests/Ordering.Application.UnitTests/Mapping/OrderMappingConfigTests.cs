using MapsterMapper;
using Ordering.Application.Orders;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.UnitTests.Mapping;

public sealed class OrderMappingConfigTests
{
    [Fact]
    public void CreateMappingConfig_AllRegisters_CompileWithoutUnmappedMembers()
    {
        var config = DependencyInjection.CreateMappingConfig();

        Should.NotThrow(() => config.Compile());
    }

    [Fact]
    public void CreateMappingConfig_AllRegisters_CompileAsQueryProjections()
    {
        var config = DependencyInjection.CreateMappingConfig();

        Should.NotThrow(() => config.CompileProjection());
    }

    [Fact]
    public void Map_PlacedOrder_FlattensMoneyAndItems()
    {
        var mapper = new Mapper(DependencyInjection.CreateMappingConfig());
        var order = Order.Place(
            "pos-001",
            "bar@example.com",
            [
                Line("GOLDEN-LAGER-350", 12.00m, 3),
                Line("AMBER-ALE-600", 27.60m, 2),
            ],
            new VolumeDiscountPolicy([new VolumeDiscountTier(MinimumQuantity: 5, Rate: 0.10m)]),
            new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero)).Value;

        var response = mapper.Map<OrderResponse>(order);

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
    public void Map_PlacedOrder_ToSummaryCountsItems()
    {
        var mapper = new Mapper(DependencyInjection.CreateMappingConfig());
        var order = Order.Place(
            "pos-001",
            "bar@example.com",
            [Line("GOLDEN-LAGER-350", 12.00m, 1), Line("AMBER-ALE-600", 27.60m, 1)],
            NoDiscountPolicy.Instance,
            DateTimeOffset.UnixEpoch).Value;

        var summary = mapper.Map<OrderSummaryResponse>(order);

        summary.ShouldBe(new OrderSummaryResponse(order.Id, OrderStatus.Pending, 2, 39.60m, "USD", DateTimeOffset.UnixEpoch, null));
    }

    private static OrderLine Line(string sku, decimal unitPrice, int quantity) =>
        new(Sku.Create(sku).Value, $"Product {sku}", Money.Create(unitPrice, "USD").Value, Quantity.Create(quantity).Value);
}
