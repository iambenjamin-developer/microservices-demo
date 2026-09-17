using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Web.Authentication;

/// <summary>
/// How tokens are signed and validated (section <c>Jwt</c>). The Gateway issues them and every
/// service validates them independently, so all of them must agree on these values.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits of key material.</summary>
    public const int MinimumSigningKeyLength = 32;

    /// <summary>
    /// Symmetric signing key. It is a real secret and is never committed: locally it comes from an Aspire
    /// parameter stored in user-secrets, in containers from an environment variable.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(MinimumSigningKeyLength)]
    public string SigningKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = "microservices-demo";

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = "microservices-demo-api";

    /// <summary>How long an issued access token stays valid.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Tolerance for clock differences between the issuer and the validating service.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);
}
