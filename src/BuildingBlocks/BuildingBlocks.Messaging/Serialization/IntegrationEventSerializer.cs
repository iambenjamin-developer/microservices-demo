using System.Text.Json;

namespace BuildingBlocks.Messaging.Serialization;

/// <summary>
/// The wire format of every integration event: camel-cased JSON, the same on both sides of the broker.
/// It is public because not every consumer owns its receive loop — the Notifications Azure Function is
/// triggered by the Functions host and still has to read a message this system produced.
/// </summary>
public static class IntegrationEventSerializer
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public static string Serialize(object integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), _options);

    public static object? Deserialize(BinaryData payload, Type eventType) =>
        JsonSerializer.Deserialize(payload.ToStream(), eventType, _options);

    public static TEvent? Deserialize<TEvent>(BinaryData payload)
        where TEvent : class =>
        Deserialize(payload, typeof(TEvent)) as TEvent;
}
