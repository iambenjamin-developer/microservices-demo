using System.Text.Json;

namespace BuildingBlocks.Messaging.Serialization;

internal static class IntegrationEventSerializer
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public static string Serialize(object integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), _options);

    public static object? Deserialize(BinaryData payload, Type eventType) =>
        JsonSerializer.Deserialize(payload.ToStream(), eventType, _options);
}
