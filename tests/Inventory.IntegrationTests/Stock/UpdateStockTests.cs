using System.Net;
using System.Net.Http.Json;
using Inventory.IntegrationTests.Infrastructure;

namespace Inventory.IntegrationTests.Stock;

[Collection(InventoryApiCollection.Name)]
public sealed class UpdateStockTests(InventoryApiFactory factory)
{
    [Fact]
    public async Task UpdateStock_Admin_SetsAvailableQuantityAndKeepsReservedPacks()
    {
        var stockItem = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 10, quantityReserved: 4);
        using var client = InventoryApi.CreateAdminClient(factory);

        // The SKU in the URL is normalized, like everywhere else in the service.
        var response = await client.PutAsJsonAsync(
            $"{InventoryApi.Route}/{stockItem.Sku.ToLowerInvariant()}",
            new { quantityAvailable = 25 },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await InventoryApi.ReadJsonAsync(response);
        body.GetProperty("sku").GetString().ShouldBe(stockItem.Sku);
        body.GetProperty("quantityAvailable").GetInt32().ShouldBe(25);
        body.GetProperty("quantityReserved").GetInt32().ShouldBe(4);
        body.GetProperty("quantityOnHand").GetInt32().ShouldBe(29);
        body.GetProperty("updatedOnUtc").GetDateTimeOffset().ShouldBeGreaterThan(stockItem.UpdatedOnUtc);

        var stored = await InventoryApi.LoadStockAsync(factory, stockItem.Sku);
        stored.QuantityAvailable.ShouldBe(25);
        stored.QuantityReserved.ShouldBe(4);
    }

    [Fact]
    public async Task UpdateStock_NegativeQuantity_Returns400AndChangesNothing()
    {
        var stockItem = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 10);
        using var client = InventoryApi.CreateAdminClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{InventoryApi.Route}/{stockItem.Sku}",
            new { quantityAvailable = -1 },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await InventoryApi.ReadProblemAsync(response);
        problem.GetProperty("errors").TryGetProperty("QuantityAvailable", out _).ShouldBeTrue();
        (await InventoryApi.LoadStockAsync(factory, stockItem.Sku)).QuantityAvailable.ShouldBe(10);
    }

    [Fact]
    public async Task UpdateStock_UnknownSku_Returns404WithErrorCode()
    {
        using var client = InventoryApi.CreateAdminClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{InventoryApi.Route}/not-stocked",
            new { quantityAvailable = 5 },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await InventoryApi.ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("Stock.NotFound");
        problem.GetProperty("detail").GetString().ShouldBe("No stock is kept for SKU 'NOT-STOCKED'.");
    }

    [Fact]
    public async Task UpdateStock_WithoutToken_Returns401()
    {
        var stockItem = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 10);
        using var client = InventoryApi.CreateAnonymousClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{InventoryApi.Route}/{stockItem.Sku}",
            new { quantityAvailable = 5 },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await InventoryApi.LoadStockAsync(factory, stockItem.Sku)).QuantityAvailable.ShouldBe(10);
    }

    [Fact]
    public async Task UpdateStock_PointOfSale_Returns403()
    {
        var stockItem = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 10);
        using var client = InventoryApi.CreatePointOfSaleClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{InventoryApi.Route}/{stockItem.Sku}",
            new { quantityAvailable = 5 },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await InventoryApi.LoadStockAsync(factory, stockItem.Sku)).QuantityAvailable.ShouldBe(10);
    }
}
