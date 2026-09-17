using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Web.Authentication;

namespace Gateway.Authentication;

/// <summary>
/// The accounts <c>POST /auth/token</c> accepts, keyed by user name (section <c>DemoUsers</c>).
/// </summary>
/// <remarks>
/// These are demo credentials on purpose, not secrets: the project must run for anyone who clones it,
/// and the account list is part of the demo script. The real secret is <see cref="JwtOptions.SigningKey"/>.
/// A production system would replace this with an identity provider (Entra ID, Keycloak, Auth0) and the
/// Gateway would keep only the token validation half.
/// </remarks>
public sealed class DemoUsersOptions
{
    public const string SectionName = "DemoUsers";

    public Dictionary<string, DemoUser> Accounts { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DemoUser
{
    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Role { get; set; } = Roles.PointOfSale;

    /// <summary>Shown by the web app; falls back to the user name.</summary>
    public string? DisplayName { get; set; }
}
