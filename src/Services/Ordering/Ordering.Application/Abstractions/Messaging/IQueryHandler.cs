using BuildingBlocks.Common.Results;

namespace Ordering.Application.Abstractions.Messaging;

/// <summary>
/// Handles one query. Implementations live in Infrastructure, next to the database, so they project straight
/// to the response (<c>AsNoTracking</c> + the <c>Project*</c> methods of the mappers) instead of materializing aggregates.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
