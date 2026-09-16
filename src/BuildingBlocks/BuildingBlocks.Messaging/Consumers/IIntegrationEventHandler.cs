using BuildingBlocks.Contracts;

namespace BuildingBlocks.Messaging.Consumers;

/// <summary>
/// Handles one integration event. Implementations change state through the service's DbContext
/// (and may stage new events in the outbox) but must <b>not</b> call <c>SaveChangesAsync</c>:
/// the consumer pipeline saves the business change and the inbox record in a single transaction.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken);
}

public sealed record IntegrationEventContext(Guid MessageId, string? CorrelationId, int DeliveryCount);
