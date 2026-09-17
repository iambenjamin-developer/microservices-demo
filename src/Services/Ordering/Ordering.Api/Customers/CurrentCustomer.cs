using System.Security.Claims;
using BuildingBlocks.Web.Authentication;

namespace Ordering.Api.Customers;

/// <summary>
/// The point of sale placing or reading orders, bound as an endpoint parameter (<see cref="BindAsync"/>)
/// from the claims of the access token the Gateway issued and this service validated.
/// </summary>
/// <remarks>
/// Identity is never taken from the request body or a header: a customer can only act as itself, which is
/// what makes "another customer's order answers 404" an authorization rule rather than a convention.
/// </remarks>
public sealed record CurrentCustomer(string Id, string Email)
{
    public static ValueTask<CurrentCustomer?> BindAsync(HttpContext context)
    {
        var id = context.User.FindFirstValue(JwtClaimNames.Subject);
        var email = context.User.FindFirstValue(JwtClaimNames.Email);

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(email))
        {
            // The endpoints require an authenticated caller, so this only happens with a token that
            // validates but does not identify a point of sale. Treat it as "not authenticated".
            throw new BadHttpRequestException(
                "The access token does not identify a point of sale.",
                StatusCodes.Status401Unauthorized);
        }

        return ValueTask.FromResult<CurrentCustomer?>(new CurrentCustomer(id, email));
    }
}
