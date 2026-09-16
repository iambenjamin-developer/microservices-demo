namespace BuildingBlocks.Messaging.Inbox;

/// <summary>
/// Records that a consumer already processed a message. Saved in the same transaction as the
/// consumer's business change, so a redelivered message is detected and skipped (idempotent consumer).
/// </summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; init; }

    public required string Consumer { get; init; }

    public DateTimeOffset ProcessedOnUtc { get; init; }
}
