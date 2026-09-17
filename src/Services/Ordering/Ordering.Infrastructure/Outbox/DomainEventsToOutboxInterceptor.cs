using BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ordering.Domain.Abstractions;

namespace Ordering.Infrastructure.Outbox;

/// <summary>
/// Before every save, turns the domain events of tracked aggregates into outbox messages, so they are committed
/// in the same transaction as the state that raised them (transactional outbox). Both paths go through here:
/// <c>IUnitOfWork.SaveChangesAsync</c> in HTTP commands and the consumer pipeline's own save.
/// </summary>
/// <remarks>Stateless, so one instance is shared by every pooled <see cref="DbContext"/>.</remarks>
internal sealed class DomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        StageDomainEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StageDomainEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void StageDomainEvents(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        var aggregates = dbContext.ChangeTracker
            .Entries<AggregateRoot<Guid>>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                // CorrelationId = aggregate id (the order id): every message of one order's saga shares it.
                dbContext.AddToOutbox(IntegrationEventMapper.ToIntegrationEvent(domainEvent), aggregate.Id.ToString());
            }

            // Cleared once staged: the outbox rows are now tracked, so a second save cannot stage them twice.
            aggregate.ClearDomainEvents();
        }
    }
}
