using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gateway.Authentication;
using Gateway.Tests.Infrastructure;

namespace Gateway.Tests.Routing;

/// <summary>
/// Authentication and authorization run before YARP forwards anything, so these assertions hold with no
/// service behind the Gateway: a request that is rejected here never reaches Catalog, Ordering or Inventory.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class ProxyAuthorizationTests(GatewayFactory factory)
{
    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/orders")]
    [InlineData("/api/stock")]
    [InlineData("/api/notifications")]
    public async Task ProxiedRoute_WithoutToken_Returns401(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminRoute_WithPointOfSaleToken_Returns403()
    {
        using var client = await SignInAsync("bar");

        var response = await client.PutAsJsonAsync(
            "/api/stock/GOLDEN-LAGER-350",
            new { quantityAvailable = 10 },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownRoute_IsNotProxied()
    {
        using var client = await SignInAsync("admin");

        var response = await client.GetAsync("/api/invoices", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NotificationsRoute_WithAWriteMethod_IsNotProxied()
    {
        // The notifications panel is read-only. The route only matches GET, so the Gateway answers 405
        // itself and the Function is never called: what a service does not expose is not proxied.
        using var client = await SignInAsync("bar");

        var response = await client.PostAsJsonAsync(
            "/api/notifications",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Preflight_FromTheWebAppOrigin_IsAnsweredWithoutAToken()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/orders");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldContain("http://localhost:5173");
    }

    [Fact]
    public async Task Preflight_FromAnUnknownOrigin_IsNotAllowed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/orders");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private async Task<HttpClient> SignInAsync(string username)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            TokenEndpoint.Route,
            new TokenRequest(username, "demo"),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }
}
