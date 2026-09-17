namespace Ordering.Application.Abstractions.Security;

/// <summary>
/// The access token of the caller currently being served, if there is one.
/// </summary>
/// <remarks>
/// Ordering calls Catalog on behalf of the point of sale that is placing the order, so the call carries the
/// caller's token instead of a service identity. The port keeps HTTP out of the inner layers: the Api project
/// reads it from the request, and Infrastructure only knows there is a token to forward.
/// </remarks>
public interface IAccessTokenProvider
{
    string? GetAccessToken();
}
