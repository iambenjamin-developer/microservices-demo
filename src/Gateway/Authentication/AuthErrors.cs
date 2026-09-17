using BuildingBlocks.Common.Results;

namespace Gateway.Authentication;

internal static class AuthErrors
{
    /// <summary>
    /// One error for "unknown user" and "wrong password": telling them apart would let a caller
    /// enumerate the accounts.
    /// </summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "User name or password is incorrect.");
}
