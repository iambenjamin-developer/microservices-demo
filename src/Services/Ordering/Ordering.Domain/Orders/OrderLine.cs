using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Orders;

/// <summary>
/// Input for <see cref="Order.Place"/>: one product with the price snapshot taken from Catalog
/// when the order is placed, so later price changes do not alter existing orders.
/// </summary>
public sealed record OrderLine(Sku Sku, string ProductName, Money UnitPrice, Quantity Quantity);
