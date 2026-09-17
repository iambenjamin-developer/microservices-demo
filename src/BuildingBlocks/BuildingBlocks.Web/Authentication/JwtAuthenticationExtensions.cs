using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Web.Authentication;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Validates the bearer token of every request. The Gateway already does this, but each service repeats it:
    /// a service must not trust a caller just because it arrived on the internal network.
    /// </summary>
    /// <remarks>
    /// Endpoints are authenticated by default (fallback policy); anything public must say so with
    /// <c>AllowAnonymous</c>. Forgetting to protect an endpoint then fails closed instead of open.
    /// </remarks>
    public static TBuilder AddJwtAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(options => options.Lifetime > TimeSpan.Zero, "Jwt:Lifetime must be positive.")
            .ValidateOnStart();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var options = jwt.Value;

                // Keep the claim names of the token ("sub", "role", ...) instead of the legacy
                // WS-Federation URIs ASP.NET Core maps them to by default.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = options.CreateSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = options.ClockSkew,
                    NameClaimType = JwtClaimNames.Subject,
                    RoleClaimType = JwtClaimNames.Role,
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(Roles.Admin));

        return builder;
    }

    /// <summary>The symmetric key used to sign and to validate tokens.</summary>
    public static SymmetricSecurityKey CreateSigningKey(this JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));
}
