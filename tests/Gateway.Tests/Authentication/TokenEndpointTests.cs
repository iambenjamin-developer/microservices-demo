using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Authentication;
using Gateway.Tests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Gateway.Tests.Authentication;

[Collection(GatewayCollection.Name)]
public sealed class TokenEndpointTests(GatewayFactory factory)
{
    [Fact]
    public async Task IssueToken_PointOfSaleCredentials_ReturnsTokenCarryingTheIdentity()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            TokenEndpoint.Route,
            new TokenRequest("bar", "demo"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        token.ShouldNotBeNull();
        token.TokenType.ShouldBe("Bearer");
        token.Role.ShouldBe("PointOfSale");
        token.ExpiresIn.ShouldBe(3600);

        // The services read the customer from these claims, so they are part of the contract.
        var claims = new JsonWebToken(token.AccessToken);
        claims.Subject.ShouldBe("bar");
        claims.GetClaim("email").Value.ShouldBe("bar@example.com");
        claims.GetClaim("role").Value.ShouldBe("PointOfSale");
        claims.Audiences.ShouldContain("microservices-demo-api");
        claims.Issuer.ShouldBe("microservices-demo");
    }

    [Fact]
    public async Task IssueToken_AdminCredentials_CarriesTheAdminRole()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            TokenEndpoint.Route,
            new TokenRequest("admin", "demo"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        token.ShouldNotBeNull();
        token.Role.ShouldBe("Admin");
    }

    [Theory]
    [InlineData("bar", "wrong-password")]
    [InlineData("nobody", "demo")]
    public async Task IssueToken_BadCredentials_Returns401WithTheSameError(string username, string password)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            TokenEndpoint.Route,
            new TokenRequest(username, password),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task IssueToken_MissingFields_Returns400()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            TokenEndpoint.Route,
            new TokenRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("errors").TryGetProperty("Username", out _).ShouldBeTrue();
        problem.GetProperty("errors").TryGetProperty("Password", out _).ShouldBeTrue();
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }
}
