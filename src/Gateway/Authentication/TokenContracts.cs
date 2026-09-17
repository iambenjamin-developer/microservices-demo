namespace Gateway.Authentication;

public sealed record TokenRequest(string? Username, string? Password);

/// <summary>OAuth2-shaped response, so a client library or the web app can consume it without surprises.</summary>
public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Username,
    string Email,
    string Role,
    string DisplayName);
