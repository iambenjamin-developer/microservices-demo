using Ordering.Application.Abstractions.Messaging;

namespace Ordering.Application.Orders.GetOrders;

/// <summary>Orders of <paramref name="CustomerId"/>, most recent first.</summary>
public sealed record GetOrdersQuery(string CustomerId) : IQuery<IReadOnlyList<OrderSummaryResponse>>;
