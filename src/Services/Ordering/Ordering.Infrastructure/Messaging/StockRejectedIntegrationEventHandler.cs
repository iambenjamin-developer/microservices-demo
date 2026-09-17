using BuildingBlocks.Contracts.Inventory;
using BuildingBlocks.Messaging.Consumers;
using Microsoft.Extensions.Logging;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Orders.RejectOrder;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Messaging;

/// <summary>
/// Inbound adapter for the compensation path. It does not save; the consumer pipeline commits the order change,
/// the <c>OrderRejected</c> outbox message and the inbox record together.
/// </summary>
internal sealed partial class StockRejectedIntegrationEventHandler(
    ICommandHandler<RejectOrderCommand, OrderStatus> rejectOrder,
    ILogger<StockRejectedIntegrationEventHandler> logger) : IIntegrationEventHandler<StockRejected>
{
    public async Task HandleAsync(StockRejected integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        var reason = integrationEvent.UnavailableSkus is { Count: > 0 } skus
            ? $"{integrationEvent.Reason} ({string.Join(", ", skus)})"
            : integrationEvent.Reason;

        var result = await rejectOrder.HandleAsync(new RejectOrderCommand(integrationEvent.OrderId, reason), cancellationToken);

        if (result.IsSuccess)
        {
            LogRejected(logger, integrationEvent.OrderId, context.MessageId);
            return;
        }

        IntegrationEventFailure.CompleteOrThrow(logger, result.Error, nameof(StockRejected), integrationEvent.OrderId, context);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} rejected (message {MessageId})")]
    private static partial void LogRejected(ILogger logger, Guid orderId, Guid messageId);
}
