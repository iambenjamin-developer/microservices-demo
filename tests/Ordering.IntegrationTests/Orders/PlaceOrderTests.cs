using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Orders;
using Ordering.Domain.Orders;
using Ordering.Infrastructure.Persistence;
using Ordering.IntegrationTests.Infrastructure;

namespace Ordering.IntegrationTests.Orders;

[Collection(OrderingApiCollection.Name)]
public sealed class PlaceOrderTests(OrderingApiFactory factory)
{
    private static readonly JsonSerializerOptions _json = OrderingApi.JsonOptions;

    [Fact]
    public async Task PlaceOrder_KnownProducts_Returns202AndSavesOrderWithOutboxMessageAtomically()
    {
        var customerId = OrderingApi.NewCustomerId();
        using var client = OrderingApi.CreateClient(factory, customerId);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new { items = new[] { new { sku = "golden-lager-350", quantity = 10 }, new { sku = "AMBER-ALE-600", quantity = 15 } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>(_json, TestContext.Current.CancellationToken))!;
        response.Headers.Location!.ToString().ShouldEndWith($"/api/orders/{order.Id}");
        order.Status.ShouldBe(OrderStatus.Pending);
        order.CustomerId.ShouldBe(customerId);
        order.Subtotal.ShouldBe(534.00m);
        order.Discount.ShouldBe(26.70m); // 25 packs reach the 5% tier from appsettings.json
        order.Total.ShouldBe(507.30m);
        order.Currency.ShouldBe("USD");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

        var saved = await dbContext.Orders.AsNoTracking().Include(o => o.Items)
            .SingleAsync(o => o.Id == order.Id, TestContext.Current.CancellationToken);
        saved.Items.Select(item => (item.Sku.Value, item.Quantity.Value, item.UnitPrice.Amount))
            .ShouldBe([("GOLDEN-LAGER-350", 10, 12.00m), ("AMBER-ALE-600", 15, 27.60m)], ignoreOrder: true);

        var outboxMessage = await dbContext.Set<OutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.CorrelationId == order.Id.ToString(), TestContext.Current.CancellationToken);
        outboxMessage.Subject.ShouldBe("OrderPlaced");
        outboxMessage.Topic.ShouldBe("order-events");
        using var payload = JsonDocument.Parse(outboxMessage.Payload);
        payload.RootElement.GetProperty("orderId").GetGuid().ShouldBe(order.Id);
        payload.RootElement.GetProperty("items").GetArrayLength().ShouldBe(2);

        // The background outbox processor publishes it with the outbox id as the message id.
        var published = await factory.EventBus.WaitForAsync(m => m.MessageId == outboxMessage.Id, TestContext.Current.CancellationToken);
        published.Subject.ShouldBe("OrderPlaced");
        published.CorrelationId.ShouldBe(order.Id.ToString());
    }

    [Fact]
    public async Task PlaceOrder_KnownProducts_SerializesStatusAsString()
    {
        // The typed client accepts numbers and strings alike, so the wire format is checked on the raw body:
        // controllers use MVC's JSON options, not the minimal API ones, and must still write enums as strings.
        using var client = OrderingApi.CreateClient(factory, OrderingApi.NewCustomerId());

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new { items = new[] { new { sku = "GOLDEN-LAGER-350", quantity = 1 } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
    }

    [Fact]
    public async Task PlaceOrder_UnknownSku_Returns400AndSavesNothing()
    {
        var customerId = OrderingApi.NewCustomerId();
        using var client = OrderingApi.CreateClient(factory, customerId);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new { items = new[] { new { sku = "GOLDEN-LAGER-350", quantity = 1 }, new { sku = "GHOST-BEER-1", quantity = 1 } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await OrderingApi.ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("Orders.UnknownProducts");

        await AssertNoOrdersAsync(customerId);
    }

    [Fact]
    public async Task PlaceOrder_InvalidItems_Returns400WithErrorsPerField()
    {
        using var client = OrderingApi.CreateClient(factory, OrderingApi.NewCustomerId());

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new { items = new[] { new { sku = "GOLDEN LAGER", quantity = 0 } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await OrderingApi.ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("Validation.Failed");
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("Items[0].Sku", out _).ShouldBeTrue();
        errors.TryGetProperty("Items[0].Quantity", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task PlaceOrder_CatalogUnavailable_Returns503AndSavesNothing()
    {
        var customerId = OrderingApi.NewCustomerId();
        using var client = OrderingApi.CreateClient(factory, customerId);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new { items = new[] { new { sku = FakeCatalogClient.UnavailableTriggerSku, quantity = 1 } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await OrderingApi.ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("Catalog.Unavailable");

        await AssertNoOrdersAsync(customerId);
    }

    private async Task AssertNoOrdersAsync(string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

        (await dbContext.Orders.AnyAsync(o => o.CustomerId == customerId, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
}
