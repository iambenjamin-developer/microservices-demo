using System.Net.Http.Headers;
using System.Text.Json;
using Inventory.Api.Domain;
using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.IntegrationTests.Infrastructure;

/// <summary>
/// HTTP and data helpers shared by the tests. A test that changes stock works on a SKU of its own
/// (<see cref="SeedStockAsync"/>), so tests never see each other's quantities and the seeded rows stay as they are.
/// </summary>
public static class InventoryApi
{
    public const string Route = "/api/stock";

    public static HttpClient CreatePointOfSaleClient(InventoryApiFactory factory) =>
        CreateClientWithToken(factory, TestTokens.ForPointOfSale());

    public static HttpClient CreateAdminClient(InventoryApiFactory factory) =>
        CreateClientWithToken(factory, TestTokens.ForAdmin());

    /// <summary>A client with no token at all: every endpoint must answer 401.</summary>
    public static HttpClient CreateAnonymousClient(InventoryApiFactory factory) => factory.CreateClient();

    public static string NewSku() => $"TEST-{Guid.NewGuid():N}".ToUpperInvariant();

    /// <summary>Inserts a stock row for a new SKU; <paramref name="quantityReserved"/> packs are reserved through the domain.</summary>
    public static async Task<StockItem> SeedStockAsync(InventoryApiFactory factory, int quantityAvailable, int quantityReserved = 0)
    {
        var utcNow = DateTimeOffset.UtcNow.AddMinutes(-5);
        var stockItem = StockItem.Create(NewSku(), quantityAvailable + quantityReserved, utcNow);
        if (quantityReserved > 0)
        {
            stockItem.Reserve(quantityReserved, utcNow);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        dbContext.StockItems.Add(stockItem);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return stockItem;
    }

    public static async Task<StockItem> LoadStockAsync(InventoryApiFactory factory, string sku)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await dbContext.StockItems.AsNoTracking().SingleAsync(item => item.Sku == sku, TestContext.Current.CancellationToken);
    }

    private static HttpClient CreateClientWithToken(InventoryApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }

    public static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        return await ReadJsonAsync(response);
    }
}
