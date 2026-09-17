using BuildingBlocks.Web.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Notifications.Api;

namespace Notifications.Authentication;

/// <summary>
/// Authentication for the HTTP triggers. The Functions host owns the ASP.NET Core pipeline, so there is no
/// <c>UseAuthentication</c> to call; the same <c>JwtBearer</c> handler the other services register is invoked
/// from a worker middleware instead.
/// </summary>
/// <remarks>
/// It fails closed like the fallback policy elsewhere: an HTTP trigger is authenticated unless it is listed in
/// <see cref="AnonymousFunctions"/>. Non-HTTP triggers (the Service Bus one) have no caller and pass through —
/// the broker is not a user, and what protects that path is the connection string, not a token.
/// </remarks>
internal sealed class JwtAuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    /// <summary>Functions that may be called without a token. Empty today; health probes would go here.</summary>
    private static readonly HashSet<string> AnonymousFunctions = new(StringComparer.Ordinal);

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.GetHttpContext();

        if (httpContext is null || AnonymousFunctions.Contains(context.FunctionDefinition.Name))
        {
            await next(context);
            return;
        }

        var authentication = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!authentication.Succeeded)
        {
            await NotificationErrors.Unauthenticated.ToProblem().ExecuteAsync(httpContext);
            return;
        }

        httpContext.User = authentication.Principal;
        await next(context);
    }
}
