using Ordering.Domain.Orders;

namespace Ordering.Application.Orders;

/// <summary>An order with its items. Money values are flattened to amounts plus a single <see cref="Currency"/>.</summary>
public sealed record OrderResponse(
    Guid Id,
    string CustomerId,
    OrderStatus Status,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string Currency,
    string? RejectionReason,
    DateTimeOffset PlacedOnUtc,
    DateTimeOffset? CompletedOnUtc);

public sealed record OrderItemResponse(
    string Sku,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

/// <summary>Row of the order list: no items, so the query does not join them.</summary>
public sealed record OrderSummaryResponse(
    Guid Id,
    OrderStatus Status,
    int ItemCount,
    decimal Total,
    string Currency,
    DateTimeOffset PlacedOnUtc,
    DateTimeOffset? CompletedOnUtc);
