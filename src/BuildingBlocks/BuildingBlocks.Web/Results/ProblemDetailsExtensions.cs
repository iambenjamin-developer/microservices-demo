using System.Diagnostics;
using BuildingBlocks.Common.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Web.Results;

public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Every error response (handled or not) is an RFC 9457 <c>application/problem+json</c> body
    /// carrying the request instance and the trace id, so a client error can be found in the traces.
    /// </summary>
    /// <remarks>
    /// MVC's <c>ProblemDetailsFactory</c> applies the same customization, so minimal APIs and controllers
    /// produce the same body.
    /// </remarks>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            context.ProblemDetails.Extensions.TryAdd(
                "traceId",
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        });

    /// <summary>
    /// Unhandled exceptions and bare status codes (e.g. 404 on an unknown route) also become ProblemDetails.
    /// Binding failures (malformed JSON, missing body) keep their 4xx status instead of turning into a 500.
    /// </summary>
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder app) =>
        app
            .UseExceptionHandler(new ExceptionHandlerOptions
            {
                StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
                    ? badRequest.StatusCode
                    : StatusCodes.Status500InternalServerError,
            })
            .UseStatusCodePages();

    /// <summary>Translates a failed <see cref="Result"/> into a ProblemDetails response.</summary>
    public static ProblemHttpResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result cannot be converted to a problem.");
        }

        return result.Error.ToProblem();
    }

    public static ProblemHttpResult ToProblem(this Error error)
    {
        var problem = ErrorProblem.From(error);

        return TypedResults.Problem(
            statusCode: problem.StatusCode,
            title: problem.Title,
            type: problem.Type,
            detail: error.Description,
            extensions: problem.Extensions);
    }
}
