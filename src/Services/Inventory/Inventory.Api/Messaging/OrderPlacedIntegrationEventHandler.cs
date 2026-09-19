using BuildingBlocks.Contracts.Inventory;
using BuildingBlocks.Contracts.Ordering;
using BuildingBlocks.Messaging.Consumers;
using BuildingBlocks.Messaging.Outbox;
using Inventory.Api.Domain;
using Inventory.Api.Services;

namespace Inventory.Api.Messaging;

/// <summary>
/// The inventory half of the choreography saga: an order was placed, so stock is reserved all-or-nothing and the
/// outcome is published back as <see cref="StockReserved"/> or <see cref="StockRejected"/>.
/// </summary>
/// <remarks>
/// The stock rule is not here: it lives in <see cref="IStockService"/>, shared with the HTTP API. This handler turns
/// the message into reservation lines and the outcome into an event.
/// No <c>SaveChangesAsync</c> here on purpose, and <see cref="IStockService.StageReservationAsync"/> does not save
/// either: the consumer pipeline commits the reserved quantities, the outcome
/// event in the outbox and the inbox record of this message in one transaction. That makes the step atomic
/// (stock is never reserved without an answer) and idempotent (a redelivered message is skipped by the inbox).
/// A concurrent reservation of the same SKU makes that save fail on the <c>xmin</c> concurrency token; the
/// message is then abandoned and retried against fresh quantities, and only after the subscription's max
/// delivery count does it go to the dead-letter queue.
/// </remarks>
internal sealed partial class OrderPlacedIntegrationEventHandler(
    IStockService stockService,
    IOutbox outbox,
    ILogger<OrderPlacedIntegrationEventHandler> logger) : IIntegrationEventHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        var lines = integrationEvent.Items
            .Select(item => new ReservationLine(item.Sku, item.Quantity))
            .ToArray();

        var outcome = await stockService.StageReservationAsync(lines, cancellationToken);

        // CorrelationId = order id: every message of one order's saga carries the same one.
        var correlationId = integrationEvent.OrderId.ToString();

        if (outcome.IsReserved)
        {
            outbox.Add(new StockReserved(integrationEvent.OrderId), correlationId);
            LogReserved(logger, integrationEvent.OrderId, lines.Length, context.MessageId);
            return;
        }

        outbox.Add(
            new StockRejected(integrationEvent.OrderId, outcome.Reason!, outcome.UnavailableSkus),
            correlationId);

        LogRejected(logger, integrationEvent.OrderId, outcome.Reason!, string.Join(", ", outcome.UnavailableSkus), context.MessageId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reserved stock for order {OrderId} ({LineCount} lines) (message {MessageId})")]
    private static partial void LogReserved(ILogger logger, Guid orderId, int lineCount, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected order {OrderId}: {Reason} [{UnavailableSkus}] (message {MessageId})")]
    private static partial void LogRejected(ILogger logger, Guid orderId, string reason, string unavailableSkus, Guid messageId);
}
