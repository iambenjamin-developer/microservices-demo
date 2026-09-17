using System.Diagnostics;
using BuildingBlocks.Contracts;
using BuildingBlocks.Messaging.Serialization;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Messaging.Outbox;

/// <summary>
/// Stages an integration event in the current <see cref="DbContext"/>. Nothing is sent here:
/// the event is committed together with the business change by the caller's <c>SaveChangesAsync</c>.
/// </summary>
public interface IOutbox
{
    void Add(IntegrationEvent integrationEvent, string? correlationId = null);
}

internal sealed class EfOutbox<TDbContext>(TDbContext dbContext) : IOutbox
    where TDbContext : DbContext
{
    public void Add(IntegrationEvent integrationEvent, string? correlationId = null) =>
        dbContext.AddToOutbox(integrationEvent, correlationId);
}

public static class OutboxDbContextExtensions
{
    /// <summary>
    /// Same as <see cref="IOutbox.Add"/> for code that already holds the <see cref="DbContext"/>,
    /// such as a <c>SaveChanges</c> interceptor that cannot depend on a service built from that context.
    /// </summary>
    public static void AddToOutbox(this DbContext dbContext, IntegrationEvent integrationEvent, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();

        dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            Topic = Topology.TopicFor(eventType),
            Subject = Topology.SubjectFor(eventType),
            Payload = IntegrationEventSerializer.Serialize(integrationEvent),
            CorrelationId = correlationId,
            TraceParent = Activity.Current?.Id,
            OccurredOnUtc = integrationEvent.OccurredOnUtc,
        });
    }
}
