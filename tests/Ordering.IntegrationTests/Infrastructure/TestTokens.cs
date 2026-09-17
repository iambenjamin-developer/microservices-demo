using BuildingBlocks.Web.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ordering.IntegrationTests.Infrastructure;

/// <summary>
/// Signs the access tokens the tests send. The Gateway is not involved: Ordering validates tokens on its
/// own, so a token signed with the same key and audience is all the service needs.
/// </summary>
public static class TestTokens
{
    /// <summary>Only ever used by the tests; the real key is a secret injected at run time.</summary>
    public const string SigningKey = "integration-tests-signing-key-with-enough-entropy";

    private const string Issuer = "microservices-demo";
    private const string Audience = "microservices-demo-api";

    private static readonly JsonWebTokenHandler TokenHandler = new();

    public static string ForCustomer(string customerId) =>
        Create(new Dictionary<string, object>
        {
            [JwtClaimNames.Subject] = customerId,
            [JwtClaimNames.Email] = $"{customerId}@example.com",
            [JwtClaimNames.Role] = Roles.PointOfSale,
        });

    /// <summary>A well-formed token that carries no identity: used to prove the endpoints reject it.</summary>
    public static string WithoutCustomerClaims() =>
        Create(new Dictionary<string, object> { [JwtClaimNames.Role] = Roles.PointOfSale });

    /// <summary>A token an attacker could forge without the signing key: the signature must not check out.</summary>
    public static string SignedWithAnotherKey() =>
        Create(
            new Dictionary<string, object> { [JwtClaimNames.Subject] = "intruder" },
            "a-completely-different-key-of-the-same-length");

    private static string Create(Dictionary<string, object> claims, string signingKey = SigningKey)
    {
        var options = new JwtOptions { SigningKey = signingKey, Issuer = Issuer, Audience = Audience };
        var issuedAt = DateTime.UtcNow;

        return TokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddHours(1),
            Claims = claims,
            SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });
    }
}
