using System.Net;
using Inventory.Api.Persistence;
using Inventory.IntegrationTests.Infrastructure;

namespace Inventory.IntegrationTests.Stock;

[Collection(InventoryApiCollection.Name)]
public sealed class GetStockTests(InventoryApiFactory factory)
{
    [Fact]
    public async Task GetStock_WithoutFilter_ReturnsEverySeededSkuSortedBySku()
    {
        using var client = InventoryApi.CreatePointOfSaleClient(factory);

        var response = await client.GetAsync(InventoryApi.Route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await InventoryApi.ReadJsonAsync(response);
        var skus = body.EnumerateArray().Select(item => item.GetProperty("sku").GetString()!).ToList();

        skus.ShouldBe([.. skus.Order(StringComparer.Ordinal)]);
        skus.ShouldBeUnique();
        foreach (var seeded in InventorySeeder.CreateStockItems(DateTimeOffset.UtcNow))
        {
            skus.ShouldContain(seeded.Sku);
        }
    }

    [Fact]
    public async Task GetStock_SkuFilter_ReturnsOnlyRequestedSkusWithQuantityOnHand()
    {
        var stockItem = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 7, quantityReserved: 3);
        using var client = InventoryApi.CreatePointOfSaleClient(factory);
        var query = $"?sku={stockItem.Sku.ToLowerInvariant()}&sku=GOLDEN-LAGER-350&sku={stockItem.Sku}&sku=NOT-STOCKED";

        var response = await client.GetAsync(InventoryApi.Route + query, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await InventoryApi.ReadJsonAsync(response)).EnumerateArray().ToList();
        items.Select(item => item.GetProperty("sku").GetString()).ShouldBe(["GOLDEN-LAGER-350", stockItem.Sku]);

        var seeded = items[1];
        seeded.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["sku", "quantityAvailable", "quantityReserved", "quantityOnHand", "updatedOnUtc"]);
        seeded.GetProperty("quantityAvailable").GetInt32().ShouldBe(7);
        seeded.GetProperty("quantityReserved").GetInt32().ShouldBe(3);
        seeded.GetProperty("quantityOnHand").GetInt32().ShouldBe(10);
        seeded.GetProperty("updatedOnUtc").GetDateTimeOffset().ShouldBe(stockItem.UpdatedOnUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetStock_OnlyUnknownSkus_ReturnsEmptyList()
    {
        using var client = InventoryApi.CreatePointOfSaleClient(factory);

        var response = await client.GetAsync($"{InventoryApi.Route}?sku=NOT-STOCKED", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await InventoryApi.ReadJsonAsync(response)).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetStock_MoreThan100Skus_Returns400ValidationProblem()
    {
        using var client = InventoryApi.CreatePointOfSaleClient(factory);
        var query = string.Join('&', Enumerable.Range(0, 101).Select(i => $"sku=SKU-{i}"));

        var response = await client.GetAsync($"{InventoryApi.Route}?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await InventoryApi.ReadProblemAsync(response);
        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("errors").TryGetProperty("Skus", out var messages).ShouldBeTrue();
        messages[0].GetString().ShouldBe("At most 100 SKUs can be requested at once.");
    }

    [Fact]
    public async Task GetStock_WithoutToken_Returns401()
    {
        using var client = InventoryApi.CreateAnonymousClient(factory);

        var response = await client.GetAsync(InventoryApi.Route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
