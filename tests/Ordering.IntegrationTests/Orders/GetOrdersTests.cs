using System.Net;
using System.Net.Http.Json;
using Ordering.Application.Orders;
using Ordering.Domain.Orders;
using Ordering.IntegrationTests.Infrastructure;

namespace Ordering.IntegrationTests.Orders;

[Collection(OrderingApiCollection.Name)]
public sealed class GetOrdersTests(OrderingApiFactory factory)
{
    [Fact]
    public async Task GetOrderById_OwnOrder_ReturnsOrderWithItemsProjectedFromDatabase()
    {
        using var client = OrderingApi.CreateClient(factory, OrderingApi.NewCustomerId());
        var placed = await PlaceAsync(client, ("SUNNY-WHEAT-330", 2), ("AMBER-ALE-600", 15));

        var order = await client.GetFromJsonAsync<OrderResponse>(
            $"/api/orders/{placed.Id}", OrderingApi.JsonOptions, TestContext.Current.CancellationToken);

        order.ShouldNotBeNull();
        order.Status.ShouldBe(OrderStatus.Pending);
        order.Total.ShouldBe(476.40m);
        order.Items.ShouldBe(
        [
            new OrderItemResponse("AMBER-ALE-600", "Amber Ale 600ml", 27.60m, 15, 414.00m),
            new OrderItemResponse("SUNNY-WHEAT-330", "Sunny Wheat 330ml", 31.20m, 2, 62.40m),
        ]);

        // Decimal equality ignores scale; the JSON must still carry cents, not "414.0000".
        order.Items.ShouldAllBe(item => item.LineTotal.Scale == 2);
    }

    [Fact]
    public async Task GetOrderById_OtherCustomersOrder_Returns404()
    {
        using var owner = OrderingApi.CreateClient(factory, OrderingApi.NewCustomerId());
        using var stranger = OrderingApi.CreateClient(factory, OrderingApi.NewCustomerId());
        var placed = await PlaceAsync(owner, ("GOLDEN-LAGER-350", 1));

        var response = await stranger.GetAsync($"/api/orders/{placed.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await OrderingApi.ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("Orders.NotFound");
    }

    [Fact]
    public async Task GetOrders_SeveralCustomers_ReturnsOnlyOwnOrdersMostRecentFirst()
    {
        using var client = OrderingApi.CreateClient(factory, OrderingApi.NewCustomerId());
        using var otherClient = OrderingApi.CreateClient(factory, OrderingApi.NewCustomerId());
        var first = await PlaceAsync(client, ("GOLDEN-LAGER-350", 1));
        var second = await PlaceAsync(client, ("GOLDEN-LAGER-350", 1), ("AMBER-ALE-600", 1));
        await PlaceAsync(otherClient, ("AMBER-ALE-600", 3));

        var orders = await client.GetFromJsonAsync<List<OrderSummaryResponse>>(
            "/api/orders", OrderingApi.JsonOptions, TestContext.Current.CancellationToken);

        orders.ShouldNotBeNull();
        orders.Select(order => order.Id).ShouldBe([second.Id, first.Id]);
        orders[0].ItemCount.ShouldBe(2);
        orders[0].Total.ShouldBe(39.60m);
        orders[0].Currency.ShouldBe("USD");
    }

    private static async Task<OrderResponse> PlaceAsync(HttpClient client, params (string Sku, int Quantity)[] items)
    {
        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new { items = items.Select(item => new { sku = item.Sku, quantity = item.Quantity }) },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(OrderingApi.JsonOptions, TestContext.Current.CancellationToken))!;
    }
}
