namespace BuildingBlocks.Messaging.Outbox;

/// <summary>
/// An integration event persisted in the same database transaction as the business change.
/// The <see cref="OutboxProcessor{TDbContext}"/> publishes it to the broker afterwards.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    public required string Topic { get; init; }

    public required string Subject { get; init; }

    public required string Payload { get; init; }

    public string? CorrelationId { get; init; }

    /// <summary>W3C trace context captured when the event was raised, so the trace continues after the async gap.</summary>
    public string? TraceParent { get; init; }

    public DateTimeOffset OccurredOnUtc { get; init; }

    public DateTimeOffset? ProcessedOnUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
