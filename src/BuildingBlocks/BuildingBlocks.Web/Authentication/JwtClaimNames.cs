namespace BuildingBlocks.Web.Authentication;

/// <summary>
/// The claims this system puts in an access token, spelled exactly as they appear in the JWT.
/// Inbound claim mapping is turned off, so these are also the names the services read.
/// </summary>
public static class JwtClaimNames
{
    /// <summary>Subject: the account name, used as the customer id in Ordering.</summary>
    public const string Subject = "sub";

    public const string Email = "email";

    public const string Role = "role";

    /// <summary>Display name, shown by the web app.</summary>
    public const string Name = "name";
}

/// <summary>Roles carried by the <c>role</c> claim.</summary>
public static class Roles
{
    /// <summary>A bar or market: browses the catalog and places orders.</summary>
    public const string PointOfSale = "PointOfSale";

    /// <summary>Back office: maintains products and stock levels.</summary>
    public const string Admin = "Admin";
}

/// <summary>Named authorization policies shared by the Gateway routes and the service endpoints.</summary>
public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
}
