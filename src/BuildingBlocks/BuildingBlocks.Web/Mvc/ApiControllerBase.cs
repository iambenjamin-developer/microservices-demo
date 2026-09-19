using BuildingBlocks.Common.Results;
using BuildingBlocks.Web.Results;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Web.Mvc;

/// <summary>
/// Base class of the services' controllers: <c>[ApiController]</c> behavior plus the translation of a failed
/// <see cref="Result"/> into the same RFC 9457 body the minimal API services return.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Answers <paramref name="error"/> as ProblemDetails. Built through MVC's <c>ProblemDetailsFactory</c>,
    /// so the <c>AddApiProblemDetails()</c> customization (instance, trace id) applies here too.
    /// </summary>
    [NonAction]
    public ObjectResult Problem(Error error)
    {
        var problem = ErrorProblem.From(error);

        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: problem.StatusCode,
            title: problem.Title,
            type: problem.Type,
            detail: error.Description);

        foreach (var (key, value) in problem.Extensions)
        {
            problemDetails.Extensions[key] = value;
        }

        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
