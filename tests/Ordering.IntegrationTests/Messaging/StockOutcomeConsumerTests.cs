using BuildingBlocks.Contracts.Inventory;
using BuildingBlocks.Messaging.Consumers;
using BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;
using Ordering.Infrastructure.Persistence;
using Ordering.IntegrationTests.Infrastructure;

namespace Ordering.IntegrationTests.Messaging;

/// <summary>
/// Runs the registered integration event handlers the way the consumer pipeline does: handler first, then one
/// <c>SaveChangesAsync</c> for the whole scope. Asserts the order transition and the outcome event in the outbox.
/// </summary>
[Collection(OrderingApiCollection.Name)]
public sealed class StockOutcomeConsumerTests(OrderingApiFactory factory)
{
    [Fact]
    public async Task StockReserved_PendingOrder_ConfirmsOrderAndStagesOrderConfirmed()
    {
        var orderId = await SeedPendingOrderAsync();

        await ConsumeAsync(new StockReserved(orderId));

        var (order, subjects) = await LoadAsync(orderId);
        order.Status.ShouldBe(OrderStatus.Confirmed);
        order.CompletedOnUtc.ShouldNotBeNull();
        subjects.ShouldBe(["OrderConfirmed", "OrderPlaced"], ignoreOrder: true);
    }

    [Fact]
    public async Task StockRejected_PendingOrder_RejectsOrderWithReasonAndStagesOrderRejected()
    {
        var orderId = await SeedPendingOrderAsync();

        await ConsumeAsync(new StockRejected(orderId, "Insufficient stock", ["GOLDEN-LAGER-350"]));

        var (order, subjects) = await LoadAsync(orderId);
        order.Status.ShouldBe(OrderStatus.Rejected);
        order.RejectionReason.ShouldBe("Insufficient stock (GOLDEN-LAGER-350)");
        subjects.ShouldBe(["OrderPlaced", "OrderRejected"], ignoreOrder: true);
    }

    [Fact]
    public async Task StockReserved_AlreadyRejectedOrder_IsIgnoredWithoutNewEvents()
    {
        var orderId = await SeedPendingOrderAsync();
        await ConsumeAsync(new StockRejected(orderId, "Insufficient stock", []));

        await Should.NotThrowAsync(() => ConsumeAsync(new StockReserved(orderId)));

        var (order, subjects) = await LoadAsync(orderId);
        order.Status.ShouldBe(OrderStatus.Rejected);
        subjects.ShouldBe(["OrderPlaced", "OrderRejected"], ignoreOrder: true);
    }

    [Fact]
    public async Task StockReserved_UnknownOrder_ThrowsSoTheMessageIsRetriedAndDeadLettered()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => ConsumeAsync(new StockReserved(Guid.NewGuid())));
    }

    private async Task ConsumeAsync<TEvent>(TEvent integrationEvent)
        where TEvent : BuildingBlocks.Contracts.IntegrationEvent
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<TEvent>>();
        var context = new IntegrationEventContext(integrationEvent.EventId, CorrelationId: null, DeliveryCount: 1);

        await handler.HandleAsync(integrationEvent, context, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedPendingOrderAsync()
    {
        var order = Order.Place(
            OrderingApi.NewCustomerId(),
            "bar@example.com",
            [new OrderLine(Sku.Create("GOLDEN-LAGER-350").Value, "Golden Lager 350ml", Money.Create(12.00m, "USD").Value, Quantity.Create(2).Value)],
            NoDiscountPolicy.Instance,
            DateTimeOffset.UtcNow).Value;

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return order.Id;
    }

    private async Task<(Order Order, List<string> OutboxSubjects)> LoadAsync(Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

        var order = await dbContext.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId, TestContext.Current.CancellationToken);
        var subjects = await dbContext.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.CorrelationId == orderId.ToString())
            .Select(m => m.Subject)
            .ToListAsync(TestContext.Current.CancellationToken);

        return (order, subjects);
    }
}
