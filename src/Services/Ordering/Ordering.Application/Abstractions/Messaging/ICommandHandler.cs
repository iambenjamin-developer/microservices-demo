using BuildingBlocks.Common.Results;

namespace Ordering.Application.Abstractions.Messaging;

/// <summary>
/// Handles one command. Callers depend on this interface, so cross-cutting concerns (validation, logging)
/// are added as decorators around the concrete handler without changing it.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
