using BuildingBlocks.Common.Results;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web.Results;

/// <summary>
/// How an <see cref="Error"/> reads as RFC 9457 ProblemDetails. Minimal APIs and MVC controllers build their
/// responses from this one table, so the same error answers the same way whichever style serves it.
/// </summary>
internal sealed record ErrorProblem(int StatusCode, string Title, string Type, Dictionary<string, object?> Extensions)
{
    public static ErrorProblem From(Error error)
    {
        var (statusCode, title, type) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Bad Request", "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized", "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found", "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict", "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            ErrorType.Unavailable => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", "https://tools.ietf.org/html/rfc9110#section-15.6.4"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "https://tools.ietf.org/html/rfc9110#section-15.6.1"),
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        // Same "errors" shape as the ValidationProblem returned by the request validation filters.
        if (error is ValidationError validationError)
        {
            extensions["errors"] = validationError.Errors;
        }

        return new ErrorProblem(statusCode, title, type, extensions);
    }
}
