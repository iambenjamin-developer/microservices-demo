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
    public void Add(IntegrationEvent integrationEvent, string? correlationId = null)
    {
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
