using Ordering.Application.Abstractions.Messaging;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.RejectOrder;

/// <summary>
/// Inventory could not reserve the stock (compensation path of the choreography saga). Returns the resulting status.
/// </summary>
public sealed record RejectOrderCommand(Guid OrderId, string Reason) : ICommand<OrderStatus>;
