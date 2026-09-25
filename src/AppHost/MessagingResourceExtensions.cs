using Aspire.Hosting.Azure;
using Azure.Provisioning.ServiceBus;
using BuildingBlocks.Contracts;
using Microsoft.Extensions.Configuration;

namespace AppHost;

/// <summary>Where the Service Bus the services talk to lives. Chosen with the <c>Messaging:Broker</c> setting.</summary>
internal enum ServiceBusBroker
{
    /// <summary>The local emulator container (the default): no Azure account needed.</summary>
    Emulator,

    /// <summary>A real namespace that Aspire provisions in the subscription configured under <c>Azure:*</c>.</summary>
    Azure,
}

internal static class MessagingResourceExtensions
{
    public const string BrokerSettingKey = "Messaging:Broker";

    /// <summary>
    /// Name of the namespace-level SAS policy created in Azure mode for docker-compose, whose containers have no
    /// Entra ID identity. It can send and listen, never manage.
    /// </summary>
    public const string ComposeAuthorizationRuleName = "compose";

    /// <summary>
    /// Adds the Service Bus resource, backed by the emulator or by a real Azure namespace, with the topology from
    /// <see cref="Topology"/>. The services only ever see the <c>messaging</c> connection, so switching brokers is
    /// an infrastructure decision made here and never a branch in their code.
    /// </summary>
    public static IResourceBuilder<AzureServiceBusResource> AddMessaging(
        this IDistributedApplicationBuilder builder,
        string containerPrefix)
    {
        var broker = GetBroker(builder.Configuration);
        var serviceBus = builder.AddAzureServiceBus(Topology.ServiceBusConnectionName);

        if (broker == ServiceBusBroker.Emulator)
        {
            RunAsEmulator(builder, serviceBus, containerPrefix);
        }
        else
        {
            ConfigureAzureNamespace(serviceBus);
        }

        AddTopology(serviceBus);
        return serviceBus;
    }

    private static ServiceBusBroker GetBroker(IConfiguration configuration)
    {
        var value = configuration[BrokerSettingKey];

        if (string.IsNullOrWhiteSpace(value))
        {
            return ServiceBusBroker.Emulator;
        }

        return Enum.TryParse<ServiceBusBroker>(value, ignoreCase: true, out var broker) && Enum.IsDefined(broker)
            ? broker
            : throw new InvalidOperationException(
                $"Unknown {BrokerSettingKey} '{value}'. Use one of: {string.Join(", ", Enum.GetNames<ServiceBusBroker>())}.");
    }

    private static void RunAsEmulator(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<AzureServiceBusResource> serviceBus,
        string containerPrefix)
    {
        serviceBus.RunAsEmulator(emulator => emulator
            .WithContainerName($"{containerPrefix}servicebus")
            .WithHostPort(5672) // fixed AMQP port so local tools (tools/servicebus-smoke.cs) can connect
            .WithLifetime(ContainerLifetime.Persistent));

        // The emulator stores its state in a SQL Server sidecar that Aspire adds as "<name>-mssql".
        // It is not exposed by RunAsEmulator, so look it up in the model to rename its container too.
        var serviceBusSql = builder.Resources.OfType<ContainerResource>()
            .SingleOrDefault(r => r.Name == $"{serviceBus.Resource.Name}-mssql")
            ?? throw new InvalidOperationException("Service Bus emulator SQL Server sidecar not found.");
        builder.CreateResourceBuilder(serviceBusSql).WithContainerName($"{containerPrefix}servicebus-sql");
    }

    // Aspire provisions the namespace (Standard tier: Basic has no topics) through Bicep on startup and grants the
    // developer running the AppHost the "Azure Service Bus Data Owner" role, so the services and the function
    // connect with their Entra ID identity and no key ever reaches them.
    private static void ConfigureAzureNamespace(IResourceBuilder<AzureServiceBusResource> serviceBus) =>
        serviceBus.ConfigureInfrastructure(infrastructure =>
        {
            var serviceBusNamespace = infrastructure.GetProvisionableResources()
                .OfType<ServiceBusNamespace>()
                .Single();

            // Aspire disables SAS keys by default. docker-compose needs one (see ComposeAuthorizationRuleName),
            // so local auth stays on and is limited to a send/listen policy instead of the root key.
            serviceBusNamespace.DisableLocalAuth = false;

            infrastructure.Add(new ServiceBusNamespaceAuthorizationRule("composeAuthorizationRule")
            {
                Parent = serviceBusNamespace,
                Name = ComposeAuthorizationRuleName,
                Rights = [ServiceBusAccessRight.Send, ServiceBusAccessRight.Listen],
            });
        });

    // Topics, subscriptions and filters come from the shared Topology, so the infrastructure and the code can never
    // disagree about names. The same model provisions the emulator and the Azure namespace.
    private static void AddTopology(IResourceBuilder<AzureServiceBusResource> serviceBus)
    {
        foreach (var topicSubscriptions in Topology.SubscriptionDefinitions.GroupBy(s => s.Topic))
        {
            var topic = serviceBus.AddServiceBusTopic(topicSubscriptions.Key);

            foreach (var definition in topicSubscriptions)
            {
                topic.AddServiceBusSubscription($"{definition.Topic}-{definition.Name}", definition.Name)
                    .WithProperties(subscription =>
                    {
                        subscription.MaxDeliveryCount = 5;
                        subscription.DeadLetteringOnMessageExpiration = true;

                        // One correlation rule per event name: rules are OR-ed, so the subscription
                        // only receives the events its consumer handles.
                        foreach (var subject in definition.Subjects)
                        {
                            subscription.Rules.Add(new AzureServiceBusRule(subject)
                            {
                                FilterType = AzureServiceBusFilterType.CorrelationFilter,
                                CorrelationFilter = new AzureServiceBusCorrelationFilter { Subject = subject },
                            });
                        }
                    });
            }
        }
    }
}
