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

    /// <summary>
    /// An existing namespace reached through <c>ConnectionStrings:messaging</c> in the AppHost user-secrets. Aspire
    /// creates nothing: the topology is applied once with <c>tools/servicebus-provision.cs</c>.
    /// </summary>
    ConnectionString,
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
    /// Adds the Service Bus resource: the emulator, a namespace Aspire provisions, or an existing namespace given by
    /// its connection string. The services only ever see the <c>messaging</c> connection, so switching brokers is an
    /// infrastructure decision made here and never a branch in their code.
    /// </summary>
    /// <remarks>
    /// Reference it from an Azure Functions project with <see cref="WithMessagingReference"/>, not <c>WithReference</c>.
    /// </remarks>
    public static IResourceBuilder<IResourceWithConnectionString> AddMessaging(
        this IDistributedApplicationBuilder builder,
        string containerPrefix)
    {
        var broker = GetBroker(builder.Configuration);

        if (broker == ServiceBusBroker.ConnectionString)
        {
            // Read from ConnectionStrings:messaging in the AppHost configuration (user-secrets). It is a secret
            // parameter: masked in the dashboard, and asked for there if it is missing.
            return builder.AddConnectionString(Topology.ServiceBusConnectionName);
        }

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
        return builder.CreateResourceBuilder<IResourceWithConnectionString>(serviceBus.Resource);
    }

    /// <summary>
    /// References the <c>messaging</c> resource from an Azure Functions project. For a Service Bus resource it uses the
    /// Functions-specific overload, which injects <c>messaging__fullyQualifiedNamespace</c> for a provisioned namespace;
    /// the generic <c>WithReference</c> would compile but inject only an endpoint the trigger cannot use. A plain
    /// connection string is injected as <c>ConnectionStrings__messaging</c>, which the trigger reads as it is.
    /// </summary>
    public static IResourceBuilder<AzureFunctionsProjectResource> WithMessagingReference(
        this IResourceBuilder<AzureFunctionsProjectResource> functions,
        IResourceBuilder<IResourceWithConnectionString> messaging) =>
        messaging.Resource is AzureServiceBusResource serviceBus
            ? functions.WithReference(functions.ApplicationBuilder.CreateResourceBuilder(serviceBus))
            : functions.WithReference(messaging);

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
                        subscription.MaxDeliveryCount = Topology.MaxDeliveryCount;
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
