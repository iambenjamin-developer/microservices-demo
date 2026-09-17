using Ordering.Application.Abstractions.Messaging;

namespace Ordering.Application.Orders.GetOrderById;

/// <summary>
/// An order of <paramref name="CustomerId"/>. Orders of other customers are reported as not found,
/// so the API does not reveal which order ids exist.
/// </summary>
public sealed record GetOrderByIdQuery(Guid OrderId, string CustomerId) : IQuery<OrderResponse>;
