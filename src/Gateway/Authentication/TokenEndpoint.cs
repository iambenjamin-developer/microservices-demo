using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using BuildingBlocks.Web.Validation;
using Gateway.Cors;
using Gateway.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Gateway.Authentication;

/// <summary>
/// Demo sign-in: exchanges a user name and password for a signed JWT. Anonymous by necessity
/// (it is how a caller gets a token) and rate limited, because it is the one endpoint worth guessing at.
/// </summary>
internal sealed class TokenEndpoint : IEndpoint
{
    public const string Route = "/auth/token";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, Results<Ok<TokenResponse>, ProblemHttpResult> (
                TokenRequest request,
                DemoTokenIssuer issuer) =>
            {
                var result = issuer.Issue(request);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
            })
            .AllowAnonymous()
            .RequireCors(CorsPolicies.Web)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithRequestValidation<TokenRequest>()
            .WithName("IssueToken")
            .WithSummary("Exchanges demo credentials for an access token.")
            .WithTags("Auth")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}
