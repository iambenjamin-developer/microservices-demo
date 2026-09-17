using BuildingBlocks.Contracts;
using BuildingBlocks.Contracts.Ordering;
using Ordering.Domain.Abstractions;
using Ordering.Domain.Orders.Events;

namespace Ordering.Infrastructure.Outbox;

/// <summary>
/// Translates internal domain events into public integration contracts (1:1). Kept by hand on purpose: the
/// contract is what other services depend on, so every field that crosses the boundary is written explicitly.
/// The domain event id is reused as the message id, so saving the same event twice cannot create two messages.
/// </summary>
internal static class IntegrationEventMapper
{
    public static IntegrationEvent ToIntegrationEvent(IDomainEvent domainEvent) => domainEvent switch
    {
        OrderPlacedDomainEvent placed => new OrderPlaced(
            placed.OrderId,
            placed.CustomerId,
            [.. placed.Items.Select(item => new OrderPlacedItem(item.Sku, item.Quantity))])
        {
            EventId = placed.EventId,
            OccurredOnUtc = placed.OccurredOnUtc,
        },

        OrderConfirmedDomainEvent confirmed => new OrderConfirmed(
            confirmed.OrderId,
            confirmed.CustomerId,
            confirmed.CustomerEmail,
            confirmed.Total.Amount,
            confirmed.Total.Currency)
        {
            EventId = confirmed.EventId,
            OccurredOnUtc = confirmed.OccurredOnUtc,
        },

        OrderRejectedDomainEvent rejected => new OrderRejected(
            rejected.OrderId,
            rejected.CustomerId,
            rejected.CustomerEmail,
            rejected.Reason)
        {
            EventId = rejected.EventId,
            OccurredOnUtc = rejected.OccurredOnUtc,
        },

        _ => throw new InvalidOperationException($"No integration event is mapped for domain event '{domainEvent.GetType().Name}'."),
    };
}
