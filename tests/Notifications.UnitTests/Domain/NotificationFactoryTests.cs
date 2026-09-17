using BuildingBlocks.Contracts.Ordering;
using Notifications.Domain;

namespace Notifications.UnitTests.Domain;

public sealed class NotificationFactoryTests
{
    private static readonly Guid _orderId = new("0199a1f0-1c2d-7abc-8def-0123456789ab");
    private static readonly DateTimeOffset _occurredOn = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _storedOn = new(2026, 9, 17, 12, 0, 3, TimeSpan.Zero);

    [Fact]
    public void FromOrderConfirmed_ConfirmedOrder_TellsTheCustomerTheTotal()
    {
        var integrationEvent = new OrderConfirmed(_orderId, "bar", "bar@example.com", 1234.5m, "USD")
        {
            OccurredOnUtc = _occurredOn,
        };

        var notification = NotificationFactory.FromOrderConfirmed(integrationEvent, _storedOn);

        notification.Type.ShouldBe(NotificationType.OrderConfirmed);
        notification.OrderId.ShouldBe(_orderId);
        notification.CustomerId.ShouldBe("bar");
        notification.CustomerEmail.ShouldBe("bar@example.com");
        notification.Title.ShouldBe("Order #0199A1F0 confirmed");
        notification.Body.ShouldContain("1,234.50 USD");
        notification.Body.ShouldContain(_orderId.ToString());
    }

    [Fact]
    public void FromOrderRejected_RejectedOrder_ExplainsWhyAndThatNothingWasReserved()
    {
        var integrationEvent = new OrderRejected(
            _orderId,
            "market",
            "market@example.com",
            "Not enough stock for some products. (MIDNIGHT-STOUT-500)")
        {
            OccurredOnUtc = _occurredOn,
        };

        var notification = NotificationFactory.FromOrderRejected(integrationEvent, _storedOn);

        notification.Type.ShouldBe(NotificationType.OrderRejected);
        notification.Title.ShouldBe("Order #0199A1F0 rejected");
        notification.Body.ShouldContain("MIDNIGHT-STOUT-500");
        notification.Body.ShouldContain("Nothing was reserved");
    }

    [Fact]
    public void FromOrderConfirmed_AnyOutcome_KeepsBothTimestamps()
    {
        var integrationEvent = new OrderConfirmed(_orderId, "bar", "bar@example.com", 10m, "USD")
        {
            OccurredOnUtc = _occurredOn,
        };

        var notification = NotificationFactory.FromOrderConfirmed(integrationEvent, _storedOn);

        // When the order was decided and when this service heard about it are different facts:
        // the gap between them is exactly what the asynchronous flow costs.
        notification.OccurredOnUtc.ShouldBe(_occurredOn);
        notification.CreatedOnUtc.ShouldBe(_storedOn);
    }

    [Fact]
    public void FromOrderConfirmed_NoEmailInTheEvent_Throws()
    {
        // The recipient comes from the token claims Ordering copied into the event. An empty one is a bug
        // upstream, not a business outcome, so it must fail loudly instead of storing an unsendable row.
        var integrationEvent = new OrderConfirmed(_orderId, "bar", "   ", 10m, "USD");

        Should.Throw<ArgumentException>(() => NotificationFactory.FromOrderConfirmed(integrationEvent, _storedOn));
    }

    [Fact]
    public void OrderReference_AnyOrderId_IsShortStableAndUpperCase()
    {
        var reference = NotificationFactory.OrderReference(_orderId);

        reference.ShouldBe("#0199A1F0");
        reference.ShouldBe(NotificationFactory.OrderReference(_orderId));
    }
}
