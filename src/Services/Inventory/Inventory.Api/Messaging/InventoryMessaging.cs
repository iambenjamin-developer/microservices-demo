using BuildingBlocks.Contracts;
using BuildingBlocks.Contracts.Ordering;
using BuildingBlocks.Messaging;
using Inventory.Api.Persistence;

namespace Inventory.Api.Messaging;

internal static class InventoryMessaging
{
    /// <summary>
    /// Asynchronous integration: consume <c>OrderPlaced</c> idempotently through the inbox and answer through
    /// the transactional outbox. Both tables live in the inventory database (database per service).
    /// </summary>
    public static IHostApplicationBuilder AddInventoryMessaging(this IHostApplicationBuilder builder)
    {
        builder.AddServiceBusMessaging();

        builder.Services.AddOutbox<InventoryDbContext>();
        builder.Services.AddIntegrationEventHandler<OrderPlaced, OrderPlacedIntegrationEventHandler>();
        builder.Services.AddServiceBusSubscription<InventoryDbContext>(Topology.Subscriptions.Inventory);

        return builder;
    }
}
