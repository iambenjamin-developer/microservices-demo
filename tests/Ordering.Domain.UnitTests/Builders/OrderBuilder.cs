using BuildingBlocks.Common.Results;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.UnitTests.Builders;

/// <summary>
/// Test Data Builder: valid defaults so each test only states what matters to it.
/// </summary>
internal sealed class OrderBuilder
{
    public static readonly DateTimeOffset DefaultPlacedOnUtc = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly List<OrderLine> _lines = [];
    private string _customerId = "pos-001";
    private string _customerEmail = "bar@example.com";
    private IDiscountPolicy _discountPolicy = NoDiscountPolicy.Instance;
    private DateTimeOffset _placedOnUtc = DefaultPlacedOnUtc;

    public static OrderBuilder AnOrder() => new();

    public OrderBuilder WithCustomer(string customerId, string customerEmail)
    {
        _customerId = customerId;
        _customerEmail = customerEmail;
        return this;
    }

    public OrderBuilder WithLine(string sku, decimal unitPrice, int quantity, string currency = "USD")
    {
        _lines.Add(Line(sku, unitPrice, quantity, currency));
        return this;
    }

    public OrderBuilder WithLines(IEnumerable<OrderLine> lines)
    {
        _lines.AddRange(lines);
        return this;
    }

    public OrderBuilder WithDiscountPolicy(IDiscountPolicy discountPolicy)
    {
        _discountPolicy = discountPolicy;
        return this;
    }

    public OrderBuilder PlacedOn(DateTimeOffset placedOnUtc)
    {
        _placedOnUtc = placedOnUtc;
        return this;
    }

    public Result<Order> Place()
    {
        var lines = _lines.Count == 0 ? [Line("GOLDEN-LAGER-350", 12.00m, 1)] : _lines;
        return Order.Place(_customerId, _customerEmail, lines, _discountPolicy, _placedOnUtc);
    }

    /// <summary>Places a valid <see cref="OrderStatus.Pending"/> order and clears its placement event.</summary>
    public Order Build()
    {
        var order = Place().Value;
        order.ClearDomainEvents();
        return order;
    }

    public static OrderLine Line(string sku, decimal unitPrice, int quantity, string currency = "USD") =>
        new(
            Sku.Create(sku).Value,
            $"Product {sku}",
            Money.Create(unitPrice, currency).Value,
            Quantity.Create(quantity).Value);
}
