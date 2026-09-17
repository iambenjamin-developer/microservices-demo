using Microsoft.Net.Http.Headers;
using Ordering.Application.Abstractions.Security;

namespace Ordering.Api.Customers;

/// <summary>Reads the bearer token of the request being served, for the calls Ordering makes on its behalf.</summary>
internal sealed class HttpContextAccessTokenProvider(IHttpContextAccessor httpContextAccessor) : IAccessTokenProvider
{
    private const string BearerPrefix = "Bearer ";

    public string? GetAccessToken()
    {
        var header = httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();

        return header is not null && header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? header[BearerPrefix.Length..].Trim()
            : null;
    }
}
