using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web.Authentication;

public static class AuthorizationEndpointExtensions
{
    /// <summary>
    /// Restricts the endpoint to the back office and documents the two answers a caller can get for
    /// not being allowed: 401 (no or invalid token) and 403 (valid token, wrong role).
    /// </summary>
    public static RouteHandlerBuilder RequireAdmin(this RouteHandlerBuilder builder) =>
        builder
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
