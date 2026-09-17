using Ordering.Application.Abstractions.Messaging;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.ConfirmOrder;

/// <summary>Inventory reserved the stock of the order. Returns the resulting status.</summary>
public sealed record ConfirmOrderCommand(Guid OrderId) : ICommand<OrderStatus>;
