namespace Ordering.Application.Abstractions.Persistence;

/// <summary>
/// Commits every change made through repositories in one database transaction. Domain events raised by the
/// changed aggregates are written to the outbox as part of that same commit.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
