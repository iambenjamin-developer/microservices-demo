using BuildingBlocks.Contracts.Inventory;
using BuildingBlocks.Contracts.Ordering;

namespace BuildingBlocks.Contracts;

/// <summary>
/// Single source of truth for the Service Bus topology: which topic each event is published to
/// and which subscriptions exist. Shared by the AppHost (to provision the emulator) and the services.
/// </summary>
public static class Topology
{
    public const string ServiceBusConnectionName = "messaging";

    public static class Topics
    {
        public const string OrderEvents = "order-events";
        public const string InventoryEvents = "inventory-events";
    }

    public static class Subscriptions
    {
        public const string Inventory = "inventory";
        public const string Ordering = "ordering";
        public const string Notifications = "notifications";
    }

    private static readonly Dictionary<Type, string> _topicsByEvent = new()
    {
        [typeof(OrderPlaced)] = Topics.OrderEvents,
        [typeof(OrderConfirmed)] = Topics.OrderEvents,
        [typeof(OrderRejected)] = Topics.OrderEvents,
        [typeof(StockReserved)] = Topics.InventoryEvents,
        [typeof(StockRejected)] = Topics.InventoryEvents,
    };

    /// <summary>Subscription name → topic and the event names (message Subject) it receives.</summary>
    public static readonly IReadOnlyList<SubscriptionDefinition> SubscriptionDefinitions =
    [
        new(Topics.OrderEvents, Subscriptions.Inventory, [nameof(OrderPlaced)]),
        new(Topics.OrderEvents, Subscriptions.Notifications, [nameof(OrderConfirmed), nameof(OrderRejected)]),
        new(Topics.InventoryEvents, Subscriptions.Ordering, [nameof(StockReserved), nameof(StockRejected)]),
    ];

    public static string TopicFor(Type eventType) =>
        _topicsByEvent.TryGetValue(eventType, out var topic)
            ? topic
            : throw new InvalidOperationException($"No topic is mapped for integration event '{eventType.Name}'.");

    /// <summary>The message Subject is the event name; subscriptions filter on it.</summary>
    public static string SubjectFor(Type eventType) => eventType.Name;
}

public sealed record SubscriptionDefinition(string Topic, string Name, IReadOnlyList<string> Subjects);
