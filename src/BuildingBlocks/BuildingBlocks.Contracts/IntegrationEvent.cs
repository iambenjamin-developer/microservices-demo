namespace BuildingBlocks.Contracts;

/// <summary>
/// Base type for every message that crosses a service boundary.
/// <see cref="EventId"/> becomes the Service Bus MessageId and is the idempotency key for consumers.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
}
