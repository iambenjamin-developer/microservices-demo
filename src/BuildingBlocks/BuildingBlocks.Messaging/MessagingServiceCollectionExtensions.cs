using BuildingBlocks.Contracts;
using BuildingBlocks.Messaging.Consumers;
using BuildingBlocks.Messaging.Diagnostics;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Registers the Service Bus client (connection string injected by Aspire), the event bus and tracing.</summary>
    public static IHostApplicationBuilder AddServiceBusMessaging(this IHostApplicationBuilder builder)
    {
        builder.AddAzureServiceBusClient(Topology.ServiceBusConnectionName);

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<IEventBus, AzureServiceBusEventBus>();
        builder.Services.TryAddSingleton<IntegrationEventHandlerRegistry>();

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(MessagingDiagnostics.ActivitySourceName));

        return builder;
    }

    /// <summary>Adds the transactional outbox for <typeparamref name="TDbContext"/> and its background publisher.</summary>
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName);
        services.AddScoped<IOutbox, EfOutbox<TDbContext>>();
        services.AddHostedService<OutboxProcessor<TDbContext>>();
        return services;
    }

    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        GetRegistry(services).Register<TEvent>();
        return services;
    }

    /// <summary>Starts consuming a subscription declared in <see cref="Topology.SubscriptionDefinitions"/>.</summary>
    public static IServiceCollection AddServiceBusSubscription<TDbContext>(this IServiceCollection services, string subscriptionName)
        where TDbContext : DbContext
    {
        var definition = Topology.SubscriptionDefinitions.SingleOrDefault(s => s.Name == subscriptionName)
            ?? throw new InvalidOperationException($"Subscription '{subscriptionName}' is not declared in {nameof(Topology)}.");

        services.AddSingleton<IHostedService>(sp =>
            ActivatorUtilities.CreateInstance<ServiceBusSubscriptionProcessor<TDbContext>>(sp, definition.Topic, definition.Name));

        return services;
    }

    // The registry is filled while services are registered, so the same instance must be shared with the container.
    private static IntegrationEventHandlerRegistry GetRegistry(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(IntegrationEventHandlerRegistry))?.ImplementationInstance
            is IntegrationEventHandlerRegistry existing)
        {
            return existing;
        }

        var registry = new IntegrationEventHandlerRegistry();
        services.RemoveAll<IntegrationEventHandlerRegistry>();
        services.AddSingleton(registry);
        return registry;
    }
}
