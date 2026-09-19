using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Web.Authentication;

/// <summary>
/// The controller form of <see cref="AuthorizationEndpointExtensions.RequireAdmin"/>: restricts an action or a
/// controller to the back office.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAdminAttribute : AuthorizeAttribute
{
    public RequireAdminAttribute() => Policy = AuthorizationPolicies.Admin;
}
