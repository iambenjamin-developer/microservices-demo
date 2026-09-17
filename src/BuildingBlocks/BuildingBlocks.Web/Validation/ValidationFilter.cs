using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web.Validation;

/// <summary>
/// Runs the FluentValidation validator of <typeparamref name="TRequest"/> before the handler.
/// Invalid input never reaches the application code and is answered with a 400 ValidationProblem.
/// </summary>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().SingleOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (validation.IsValid)
        {
            return await next(context);
        }

        return TypedResults.ValidationProblem(validation.ToDictionary());
    }
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder) =>
        builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
