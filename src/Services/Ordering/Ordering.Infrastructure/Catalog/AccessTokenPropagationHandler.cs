using System.Net.Http.Headers;
using Ordering.Application.Abstractions.Security;

namespace Ordering.Infrastructure.Catalog;

/// <summary>
/// Puts the caller's bearer token on the outgoing Catalog request (token relay). Catalog authenticates every
/// request, including the ones coming from another service: being inside the cluster is not a credential.
/// </summary>
internal sealed class AccessTokenPropagationHandler(IAccessTokenProvider accessTokenProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = accessTokenProvider.GetAccessToken();

        if (request.Headers.Authorization is null && !string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
