using System.Globalization;
using BuildingBlocks.Contracts.Ordering;

namespace Notifications.Domain;

/// <summary>
/// Turns an order outcome event into the message a point of sale reads. Pure and static: composing the
/// text is the only real logic in this service, so it is kept away from the database and the broker
/// and covered by unit tests.
/// </summary>
public static class NotificationFactory
{
    public static Notification FromOrderConfirmed(OrderConfirmed integrationEvent, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var reference = OrderReference(integrationEvent.OrderId);
        var total = integrationEvent.Total.ToString("N2", CultureInfo.InvariantCulture);

        return Notification.Create(
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            integrationEvent.CustomerEmail,
            NotificationType.OrderConfirmed,
            $"Order {reference} confirmed",
            $"Your order {reference} was confirmed and the stock is reserved. "
                + $"Total: {total} {integrationEvent.Currency}. "
                + $"Order id: {integrationEvent.OrderId}.",
            integrationEvent.OccurredOnUtc,
            utcNow);
    }

    public static Notification FromOrderRejected(OrderRejected integrationEvent, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var reference = OrderReference(integrationEvent.OrderId);

        return Notification.Create(
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            integrationEvent.CustomerEmail,
            NotificationType.OrderRejected,
            $"Order {reference} rejected",
            $"Your order {reference} could not be fulfilled. Reason: {integrationEvent.Reason} "
                + $"Nothing was reserved and nothing will be charged. "
                + $"Order id: {integrationEvent.OrderId}.",
            integrationEvent.OccurredOnUtc,
            utcNow);
    }

    /// <summary>A short, quotable reference for humans; the full id stays in the body for support.</summary>
    public static string OrderReference(Guid orderId) => $"#{orderId.ToString("N")[..8].ToUpperInvariant()}";
}
