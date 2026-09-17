using BuildingBlocks.Common.Results;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.ConfirmOrder;

/// <summary>
/// Does not commit: it runs inside the caller's unit of work. The integration event consumer saves the status
/// change, the <c>OrderConfirmed</c> outbox message and its inbox record in one transaction.
/// </summary>
internal sealed class ConfirmOrderCommandHandler(
    IOrderRepository orderRepository,
    TimeProvider timeProvider) : ICommandHandler<ConfirmOrderCommand, OrderStatus>
{
    public async Task<Result<OrderStatus>> HandleAsync(ConfirmOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderErrors.NotFound(command.OrderId);
        }

        var result = order.Confirm(timeProvider.GetUtcNow());
        return result.IsSuccess ? order.Status : result.Error;
    }
}
