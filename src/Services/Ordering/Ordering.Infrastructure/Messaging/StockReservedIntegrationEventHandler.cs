using BuildingBlocks.Contracts.Inventory;
using BuildingBlocks.Messaging.Consumers;
using Microsoft.Extensions.Logging;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Orders.ConfirmOrder;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Messaging;

/// <summary>
/// Inbound adapter: translates the integration event into an application command. It does not save; the consumer
/// pipeline commits the order change, the <c>OrderConfirmed</c> outbox message and the inbox record together.
/// </summary>
internal sealed partial class StockReservedIntegrationEventHandler(
    ICommandHandler<ConfirmOrderCommand, OrderStatus> confirmOrder,
    ILogger<StockReservedIntegrationEventHandler> logger) : IIntegrationEventHandler<StockReserved>
{
    public async Task HandleAsync(StockReserved integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        var result = await confirmOrder.HandleAsync(new ConfirmOrderCommand(integrationEvent.OrderId), cancellationToken);

        if (result.IsSuccess)
        {
            LogConfirmed(logger, integrationEvent.OrderId, context.MessageId);
            return;
        }

        IntegrationEventFailure.CompleteOrThrow(logger, result.Error, nameof(StockReserved), integrationEvent.OrderId, context);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} confirmed (message {MessageId})")]
    private static partial void LogConfirmed(ILogger logger, Guid orderId, Guid messageId);
}
