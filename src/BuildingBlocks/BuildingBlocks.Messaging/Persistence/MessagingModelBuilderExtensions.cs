using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Messaging.Persistence;

public static class MessagingModelBuilderExtensions
{
    public const string OutboxTable = "outbox_messages";
    public const string InboxTable = "inbox_messages";

    /// <summary>Adds the outbox and inbox tables to a service's own database (database per service).</summary>
    public static ModelBuilder AddMessagingTables(this ModelBuilder modelBuilder) =>
        modelBuilder.AddOutboxTable().AddInboxTable();

    /// <summary>Only for services that publish events; a pure consumer does not need the table.</summary>
    public static ModelBuilder AddOutboxTable(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable(OutboxTable);
            outbox.HasKey(m => m.Id);
            outbox.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
            outbox.Property(m => m.Topic).HasColumnName("topic").HasMaxLength(100);
            outbox.Property(m => m.Subject).HasColumnName("subject").HasMaxLength(200);
            outbox.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb");
            outbox.Property(m => m.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            outbox.Property(m => m.TraceParent).HasColumnName("trace_parent").HasMaxLength(100);
            outbox.Property(m => m.OccurredOnUtc).HasColumnName("occurred_on_utc");
            outbox.Property(m => m.ProcessedOnUtc).HasColumnName("processed_on_utc");
            outbox.Property(m => m.Attempts).HasColumnName("attempts");
            outbox.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(2000);

            // Partial index: the processor only ever looks at pending messages.
            outbox.HasIndex(m => m.OccurredOnUtc)
                .HasDatabaseName("ix_outbox_messages_pending")
                .HasFilter("processed_on_utc IS NULL");
        });

        return modelBuilder;
    }

    /// <summary>The idempotency key of every consumer: one row per message and consumer.</summary>
    public static ModelBuilder AddInboxTable(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<InboxMessage>(inbox =>
        {
            inbox.ToTable(InboxTable);
            inbox.HasKey(m => new { m.MessageId, m.Consumer });
            inbox.Property(m => m.MessageId).HasColumnName("message_id");
            inbox.Property(m => m.Consumer).HasColumnName("consumer").HasMaxLength(100);
            inbox.Property(m => m.ProcessedOnUtc).HasColumnName("processed_on_utc");
        });

        return modelBuilder;
    }
}
