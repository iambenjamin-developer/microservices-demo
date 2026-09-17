using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ordering.IntegrationTests.Infrastructure;

/// <summary>HTTP helpers shared by the API tests. Each test uses its own customer id, so tests never see each other's orders.</summary>
public static class OrderingApi
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string NewCustomerId() => $"test-{Guid.NewGuid():N}";

    /// <summary>A client signed in as <paramref name="customerId"/>; the identity travels in the token, not in a header.</summary>
    public static HttpClient CreateClient(OrderingApiFactory factory, string customerId) =>
        CreateClientWithToken(factory, TestTokens.ForCustomer(customerId));

    public static HttpClient CreateClientWithToken(OrderingApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>A client with no token at all: every endpoint must answer 401.</summary>
    public static HttpClient CreateAnonymousClient(OrderingApiFactory factory) => factory.CreateClient();

    public static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }
}
