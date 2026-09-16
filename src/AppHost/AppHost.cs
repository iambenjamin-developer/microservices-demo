using Aspire.Hosting.Azure;
using BuildingBlocks.Contracts;

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL: one server container, one database per service (database-per-service pattern).
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgWeb()
    .WithLifetime(ContainerLifetime.Persistent);

// Services are added phase by phase and will reference these databases.
postgres.AddDatabase("catalogdb");
postgres.AddDatabase("orderingdb");
postgres.AddDatabase("inventorydb");
postgres.AddDatabase("notificationsdb");

// Azure Service Bus emulator. Topics, subscriptions and filters come from the shared Topology,
// so the infrastructure and the code can never disagree about names.
var serviceBus = builder.AddAzureServiceBus(Topology.ServiceBusConnectionName)
    .RunAsEmulator(emulator => emulator
        .WithHostPort(5672) // fixed AMQP port so local tools (tools/servicebus-smoke.cs) can connect
        .WithLifetime(ContainerLifetime.Persistent));

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

builder.Build().Run();
