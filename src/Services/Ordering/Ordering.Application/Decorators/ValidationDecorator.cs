using BuildingBlocks.Common.Results;
using FluentValidation;
using FluentValidation.Results;
using Ordering.Application.Abstractions.Messaging;

namespace Ordering.Application.Decorators;

/// <summary>
/// Decorator: runs every FluentValidation validator of <typeparamref name="TCommand"/> and returns a
/// <see cref="ValidationError"/> without calling the handler when the input is invalid. It lives in the
/// application layer, so it protects a command no matter who sends it (HTTP endpoint, consumer, test).
/// </summary>
internal sealed class ValidationDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            var validation = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(validation.Errors);
        }

        if (failures.Count == 0)
        {
            return await inner.HandleAsync(command, cancellationToken);
        }

        var errors = failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(),
                StringComparer.Ordinal);

        return new ValidationError(errors);
    }
}
