using System.Security.Claims;
using BuildingBlocks.Common.Results;
using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace Notifications.Api;

/// <summary>
/// <c>GET /api/notifications</c> — what the web panel polls. The Functions host maps HTTP triggers under the
/// <c>api</c> route prefix, so the route here is the same path the other services expose.
/// </summary>
internal sealed class GetNotificationsFunction(GetNotificationsQuery query)
{
    public const string Route = "notifications";

    [Function(nameof(GetNotifications))]
    public async Task<IResult> GetNotifications(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)] HttpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // AuthorizationLevel.Anonymous only turns off the Functions host key; the bearer token was already
        // validated by the worker middleware, exactly like the fallback policy in the other services.
        var customerId = request.HttpContext.User.FindFirstValue(JwtClaimNames.Subject);
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return NotificationErrors.Unidentified.ToProblem();
        }

        var notifications = await query.ExecuteAsync(customerId, cancellationToken);
        return Results.Ok(notifications);
    }
}

internal static class NotificationErrors
{
    /// <summary>A token that validates but names no point of sale cannot be answered with somebody's data.</summary>
    public static readonly Error Unidentified = Error.Unauthorized(
        "Notifications.Unidentified",
        "The access token does not identify a point of sale.");

    public static readonly Error Unauthenticated = Error.Unauthorized(
        "Notifications.Unauthenticated",
        "A valid access token is required.");
}
