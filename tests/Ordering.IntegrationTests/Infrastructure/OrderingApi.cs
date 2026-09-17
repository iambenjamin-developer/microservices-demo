using System.Text.Json;
using System.Text.Json.Serialization;
using Ordering.Api.Customers;

namespace Ordering.IntegrationTests.Infrastructure;

/// <summary>HTTP helpers shared by the API tests. Each test uses its own customer id, so tests never see each other's orders.</summary>
public static class OrderingApi
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string NewCustomerId() => $"test-{Guid.NewGuid():N}";

    public static HttpClient CreateClient(OrderingApiFactory factory, string customerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CurrentCustomer.IdHeader, customerId);
        client.DefaultRequestHeaders.Add(CurrentCustomer.EmailHeader, $"{customerId}@example.com");
        return client;
    }

    public static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }
}
