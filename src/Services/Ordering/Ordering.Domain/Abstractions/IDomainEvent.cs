namespace Ordering.Domain.Abstractions;

/// <summary>
/// Something meaningful that happened inside the domain. Domain events stay in-process;
/// the application layer translates them into integration events stored in the outbox.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Unique id, reused as the outbox message id so a retried save never publishes twice.</summary>
    Guid EventId { get; }

    DateTimeOffset OccurredOnUtc { get; }
}

public abstract record DomainEvent(DateTimeOffset OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
}
