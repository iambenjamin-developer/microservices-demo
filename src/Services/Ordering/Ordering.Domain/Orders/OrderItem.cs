using Ordering.Domain.Abstractions;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Orders;

/// <summary>A line of an <see cref="Order"/>. Only the aggregate root creates items.</summary>
public sealed class OrderItem : Entity<Guid>
{
    private OrderItem(Guid id, Sku sku, string productName, Money unitPrice, Quantity quantity)
        : base(id)
    {
        Sku = sku;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    // Required by EF Core materialization.
    private OrderItem()
    {
        Sku = null!;
        ProductName = null!;
        UnitPrice = null!;
        Quantity = null!;
    }

    public Sku Sku { get; private set; }

    public string ProductName { get; private set; }

    public Money UnitPrice { get; private set; }

    public Quantity Quantity { get; private set; }

    public Money LineTotal => UnitPrice.Multiply(Quantity.Value);

    internal static OrderItem From(OrderLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(line.Sku);
        ArgumentNullException.ThrowIfNull(line.UnitPrice);
        ArgumentNullException.ThrowIfNull(line.Quantity);
        ArgumentException.ThrowIfNullOrWhiteSpace(line.ProductName);

        return new OrderItem(Guid.CreateVersion7(), line.Sku, line.ProductName.Trim(), line.UnitPrice, line.Quantity);
    }
}
