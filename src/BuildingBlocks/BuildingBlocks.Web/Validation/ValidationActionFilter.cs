using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace BuildingBlocks.Web.Validation;

/// <summary>
/// The MVC counterpart of <see cref="ValidationFilter{TRequest}"/>: every action argument that has a registered
/// FluentValidation validator is checked before the action runs, and invalid input is answered with the same
/// 400 ValidationProblem (field → messages).
/// </summary>
/// <remarks>
/// Registered globally by <c>AddApiControllers()</c>, so a controller cannot forget it: adding a validator to the
/// container is enough to protect every action that takes that request.
/// </remarks>
internal sealed class ValidationActionFilter(ProblemDetailsFactory problemDetailsFactory) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (httpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validation = await validator.ValidateAsync(new ValidationContext<object>(argument), httpContext.RequestAborted);
            foreach (var failure in validation.Errors)
            {
                context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }
        }

        if (context.ModelState.IsValid)
        {
            await next();
            return;
        }

        var problem = problemDetailsFactory.CreateValidationProblemDetails(httpContext, context.ModelState);
        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
    }
}
