namespace BuildingBlocks.Messaging;

/// <summary>
/// Transport abstraction (Adapter pattern). Only the outbox processor publishes through it, so business code
/// never talks to the broker directly. Swapping Azure Service Bus for RabbitMQ or Kafka means a new adapter.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}

public sealed record OutgoingMessage(
    Guid MessageId,
    string Topic,
    string Subject,
    string Payload,
    string? CorrelationId,
    string? TraceParent);
