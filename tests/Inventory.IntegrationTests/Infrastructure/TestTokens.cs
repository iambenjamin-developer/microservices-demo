using BuildingBlocks.Web.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.IntegrationTests.Infrastructure;

/// <summary>
/// Signs the access tokens the tests send. The Gateway is not involved: Inventory validates tokens on its
/// own, so a token signed with the same key and audience is all the service needs.
/// </summary>
public static class TestTokens
{
    /// <summary>Only ever used by the tests; the real key is a secret injected at run time.</summary>
    public const string SigningKey = "integration-tests-signing-key-with-enough-entropy";

    private const string Issuer = "microservices-demo";
    private const string Audience = "microservices-demo-api";

    private static readonly JsonWebTokenHandler TokenHandler = new();

    public static string ForPointOfSale() => ForRole("bar", Roles.PointOfSale);

    public static string ForAdmin() => ForRole("admin", Roles.Admin);

    private static string ForRole(string subject, string role)
    {
        var options = new JwtOptions { SigningKey = SigningKey, Issuer = Issuer, Audience = Audience };
        var issuedAt = DateTime.UtcNow;

        return TokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddHours(1),
            Claims = new Dictionary<string, object>
            {
                [JwtClaimNames.Subject] = subject,
                [JwtClaimNames.Email] = $"{subject}@example.com",
                [JwtClaimNames.Role] = role,
            },
            SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });
    }
}
