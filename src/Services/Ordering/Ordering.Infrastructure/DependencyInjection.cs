using BuildingBlocks.Contracts;
using BuildingBlocks.Contracts.Inventory;
using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Abstractions.Persistence;
using Ordering.Application.Orders;
using Ordering.Application.Orders.GetOrderById;
using Ordering.Application.Orders.GetOrders;
using Ordering.Application.Pricing;
using Ordering.Domain.Orders;
using Ordering.Infrastructure.Catalog;
using Ordering.Infrastructure.Messaging;
using Ordering.Infrastructure.Outbox;
using Ordering.Infrastructure.Persistence;
using Ordering.Infrastructure.Queries;

namespace Ordering.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddOrderingInfrastructure(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<PricingOptions>().BindConfiguration(PricingOptions.SectionName);

        // Persistence: Aspire adds pooling, retries on transient failures, health check and tracing.
        builder.AddNpgsqlDbContext<OrderingDbContext>(
            OrderingDbContext.ConnectionName,
            configureDbContextOptions: options => options.AddInterceptors(new DomainEventsToOutboxInterceptor()));

        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<OrderingDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Read side.
        services.AddScoped<IQueryHandler<GetOrderByIdQuery, OrderResponse>, GetOrderByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummaryResponse>>, GetOrdersQueryHandler>();

        // Synchronous dependency, behind an explicit resilience pipeline.
        services.AddCatalogClient();

        // Asynchronous integration: publish through the outbox, consume idempotently through the inbox.
        builder.AddServiceBusMessaging();
        services.AddOutbox<OrderingDbContext>();
        services.AddIntegrationEventHandler<StockReserved, StockReservedIntegrationEventHandler>();
        services.AddIntegrationEventHandler<StockRejected, StockRejectedIntegrationEventHandler>();
        services.AddServiceBusSubscription<OrderingDbContext>(Topology.Subscriptions.Ordering);

        return builder;
    }
}
