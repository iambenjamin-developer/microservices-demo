using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Common.Results;
using BuildingBlocks.Web.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Authentication;

/// <summary>
/// Checks demo credentials and signs the access token the rest of the system trusts.
/// This is the only place in the solution that creates a token; every other project only validates one.
/// </summary>
internal sealed class DemoTokenIssuer(
    IOptions<DemoUsersOptions> users,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider)
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public Result<TokenResponse> Issue(TokenRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;

        if (!users.Value.Accounts.TryGetValue(username, out var account)
            || !PasswordMatches(account.Password, request.Password))
        {
            return Result.Failure<TokenResponse>(AuthErrors.InvalidCredentials);
        }

        var options = jwtOptions.Value;
        var issuedAt = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = issuedAt.Add(options.Lifetime);
        var displayName = string.IsNullOrWhiteSpace(account.DisplayName) ? username : account.DisplayName;

        var accessToken = TokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                [JwtClaimNames.Subject] = username,
                [JwtClaimNames.Email] = account.Email,
                [JwtClaimNames.Role] = account.Role,
                [JwtClaimNames.Name] = displayName,
            },
            SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return Result.Success(new TokenResponse(
            accessToken,
            JwtBearerDefaults.AuthenticationScheme,
            (int)options.Lifetime.TotalSeconds,
            username,
            account.Email,
            account.Role,
            displayName));
    }

    /// <summary>Constant-time comparison so the answer does not depend on how much of the password is right.</summary>
    private static bool PasswordMatches(string expected, string? provided) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided ?? string.Empty));
}
