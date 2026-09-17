using Ordering.Application.Abstractions.Messaging;

namespace Ordering.Application.Orders.PlaceOrder;

/// <summary>
/// Places an order for the current customer. Only SKUs and quantities come from the client:
/// product names and prices are snapshotted from Catalog, so a client cannot choose its own price.
/// </summary>
public sealed record PlaceOrderCommand(
    string CustomerId,
    string CustomerEmail,
    IReadOnlyList<PlaceOrderItem> Items) : ICommand<OrderResponse>;

public sealed record PlaceOrderItem(string Sku, int Quantity);
