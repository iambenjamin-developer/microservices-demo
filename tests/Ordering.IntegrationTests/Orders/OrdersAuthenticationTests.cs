using System.Net;
using System.Net.Http.Json;
using Ordering.IntegrationTests.Infrastructure;

namespace Ordering.IntegrationTests.Orders;

/// <summary>
/// The service validates tokens itself instead of trusting the Gateway, so these run against Ordering alone.
/// </summary>
[Collection(OrderingApiCollection.Name)]
public sealed class OrdersAuthenticationTests(OrderingApiFactory factory)
{
    [Fact]
    public async Task GetOrders_WithoutToken_Returns401()
    {
        using var client = OrderingApi.CreateAnonymousClient(factory);

        var response = await client.GetAsync("/api/orders", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlaceOrder_WithoutToken_Returns401()
    {
        using var client = OrderingApi.CreateAnonymousClient(factory);
        var request = new { items = new[] { new { sku = "GOLDEN-LAGER-350", quantity = 1 } } };

        var response = await client.PostAsJsonAsync("/api/orders", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrders_WithTokenSignedByAnotherKey_Returns401()
    {
        using var client = OrderingApi.CreateClientWithToken(factory, TestTokens.SignedWithAnotherKey());

        var response = await client.GetAsync("/api/orders", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrders_TokenWithoutCustomerClaims_Returns401()
    {
        using var client = OrderingApi.CreateClientWithToken(factory, TestTokens.WithoutCustomerClaims());

        var response = await client.GetAsync("/api/orders", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
