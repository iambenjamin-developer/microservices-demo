using BuildingBlocks.Contracts;
using BuildingBlocks.Contracts.Inventory;
using BuildingBlocks.Contracts.Ordering;
using BuildingBlocks.Messaging.Consumers;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Serialization;
using Inventory.Api.Domain;
using Inventory.Api.Persistence;
using Inventory.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.IntegrationTests.Messaging;

/// <summary>
/// Runs the registered <c>OrderPlaced</c> handler the way the consumer pipeline does: handler first, then one
/// <c>SaveChangesAsync</c> for the whole scope. Asserts the stock rows and the outcome event staged in the outbox.
/// </summary>
[Collection(InventoryApiCollection.Name)]
public sealed class OrderPlacedConsumerTests(InventoryApiFactory factory)
{
    [Fact]
    public async Task OrderPlaced_WithinStock_ReservesEveryLineAndStagesStockReserved()
    {
        var first = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 10);
        var second = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 5);
        var orderId = Guid.NewGuid();

        // A repeated SKU is added up; SKUs are normalized.
        await ConsumeAsync(new OrderPlaced(orderId, "bar",
        [
            new OrderPlacedItem(first.Sku.ToLowerInvariant(), 3),
            new OrderPlacedItem(second.Sku, 5),
            new OrderPlacedItem(first.Sku, 2),
        ]));

        var firstStored = await InventoryApi.LoadStockAsync(factory, first.Sku);
        firstStored.QuantityAvailable.ShouldBe(5);
        firstStored.QuantityReserved.ShouldBe(5);
        var secondStored = await InventoryApi.LoadStockAsync(factory, second.Sku);
        secondStored.QuantityAvailable.ShouldBe(0);
        secondStored.QuantityReserved.ShouldBe(5);

        var message = (await LoadOutboxAsync(orderId)).ShouldHaveSingleItem();
        message.Topic.ShouldBe(Topology.Topics.InventoryEvents);
        message.Subject.ShouldBe(nameof(StockReserved));
        IntegrationEventSerializer.Deserialize<StockReserved>(BinaryData.FromString(message.Payload))!.OrderId.ShouldBe(orderId);
    }

    [Fact]
    public async Task OrderPlaced_InsufficientAndUnknownSkus_ReservesNothingAndStagesStockRejected()
    {
        var available = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 10);
        var scarce = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 2);
        var unknownSku = InventoryApi.NewSku();
        var orderId = Guid.NewGuid();

        await ConsumeAsync(new OrderPlaced(orderId, "bar",
        [
            new OrderPlacedItem(available.Sku, 3),
            new OrderPlacedItem(scarce.Sku, 5),
            new OrderPlacedItem(unknownSku, 1),
        ]));

        // All or nothing: the line that fits is not reserved either.
        (await InventoryApi.LoadStockAsync(factory, available.Sku)).QuantityReserved.ShouldBe(0);
        (await InventoryApi.LoadStockAsync(factory, scarce.Sku)).QuantityAvailable.ShouldBe(2);

        var message = (await LoadOutboxAsync(orderId)).ShouldHaveSingleItem();
        message.Topic.ShouldBe(Topology.Topics.InventoryEvents);
        message.Subject.ShouldBe(nameof(StockRejected));
        var rejected = IntegrationEventSerializer.Deserialize<StockRejected>(BinaryData.FromString(message.Payload))!;
        rejected.OrderId.ShouldBe(orderId);
        rejected.Reason.ShouldBe(StockReservation.UnknownSkuAndInsufficientStockReason);
        rejected.UnavailableSkus.ShouldBe([.. new[] { scarce.Sku, unknownSku }.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task OrderPlaced_InsufficientStock_StagesStockRejectedWithInsufficientStockReason()
    {
        var scarce = await InventoryApi.SeedStockAsync(factory, quantityAvailable: 1);
        var orderId = Guid.NewGuid();

        await ConsumeAsync(new OrderPlaced(orderId, "bar", [new OrderPlacedItem(scarce.Sku, 2)]));

        var message = (await LoadOutboxAsync(orderId)).ShouldHaveSingleItem();
        var rejected = IntegrationEventSerializer.Deserialize<StockRejected>(BinaryData.FromString(message.Payload))!;
        rejected.Reason.ShouldBe(StockReservation.InsufficientStockReason);
        rejected.UnavailableSkus.ShouldBe([scarce.Sku]);
        (await InventoryApi.LoadStockAsync(factory, scarce.Sku)).QuantityAvailable.ShouldBe(1);
    }

    private async Task ConsumeAsync(OrderPlaced integrationEvent)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<OrderPlaced>>();
        var context = new IntegrationEventContext(integrationEvent.EventId, integrationEvent.OrderId.ToString(), DeliveryCount: 1);

        await handler.HandleAsync(integrationEvent, context, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<OutboxMessage>> LoadOutboxAsync(Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await dbContext.Set<OutboxMessage>().AsNoTracking()
            .Where(message => message.CorrelationId == orderId.ToString())
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
