using Aspire.Hosting.Azure;
using BuildingBlocks.Contracts;

var builder = DistributedApplication.CreateBuilder(args);

// Fixed Docker container names (instead of Aspire's "<resource>-<hash>") so the containers are
// easy to find in Docker Desktop. The shared prefix keeps them grouped when sorted by name.
const string ContainerPrefix = "msdemo-";

// Configuration key "Jwt:SigningKey" in the environment variable form services read it from.
const string JwtSigningKeyVariable = "Jwt__SigningKey";

// PostgreSQL: one server container, one database per service (database-per-service pattern).
var postgres = builder.AddPostgres("postgres")
    .WithContainerName($"{ContainerPrefix}postgres")
    .WithDataVolume()
    .WithPgWeb(pgWeb => pgWeb.WithContainerName($"{ContainerPrefix}pgweb"))
    .WithLifetime(ContainerLifetime.Persistent);

// Services are added phase by phase and will reference these databases.
var catalogDb = postgres.AddDatabase("catalogdb");
var orderingDb = postgres.AddDatabase("orderingdb");
var inventoryDb = postgres.AddDatabase("inventorydb");
postgres.AddDatabase("notificationsdb");

// Azure Service Bus emulator. Topics, subscriptions and filters come from the shared Topology,
// so the infrastructure and the code can never disagree about names.
var serviceBus = builder.AddAzureServiceBus(Topology.ServiceBusConnectionName)
    .RunAsEmulator(emulator => emulator
        .WithContainerName($"{ContainerPrefix}servicebus")
        .WithHostPort(5672) // fixed AMQP port so local tools (tools/servicebus-smoke.cs) can connect
        .WithLifetime(ContainerLifetime.Persistent));

// The emulator stores its state in a SQL Server sidecar that Aspire adds as "<name>-mssql".
// It is not exposed by RunAsEmulator, so look it up in the model to rename its container too.
var serviceBusSql = builder.Resources.OfType<ContainerResource>()
    .SingleOrDefault(r => r.Name == $"{serviceBus.Resource.Name}-mssql")
    ?? throw new InvalidOperationException("Service Bus emulator SQL Server sidecar not found.");
builder.CreateResourceBuilder(serviceBusSql).WithContainerName($"{ContainerPrefix}servicebus-sql");

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

// The Gateway signs access tokens and every service validates them, so all of them need the same key.
// It is generated on first run and persisted to the AppHost user-secrets: a real secret, never committed.
var jwtSigningKey = builder.AddParameter(
    "jwt-signing-key",
    new GenerateParameterDefault { MinLength = 64, Special = false },
    secret: true,
    persist: true);

// Resource names double as service discovery names (e.g. Ordering calls "https+http://catalog").
var catalog = builder.AddProject<Projects.Catalog_Api>("catalog")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithEnvironment(JwtSigningKeyVariable, jwtSigningKey);

// Ordering does not wait for Catalog on purpose: calls to it go through a resilience pipeline (retry,
// circuit breaker, timeouts), and a missing Catalog only fails order placement with a 503.
var ordering = builder.AddProject<Projects.Ordering_Api>("ordering")
    .WithReference(orderingDb)
    .WaitFor(orderingDb)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithReference(catalog)
    .WithEnvironment(JwtSigningKeyVariable, jwtSigningKey);

// Inventory has no synchronous callers: it only reserves stock for the orders it receives from the topic.
var inventory = builder.AddProject<Projects.Inventory_Api>("inventory")
    .WithReference(inventoryDb)
    .WaitFor(inventoryDb)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithEnvironment(JwtSigningKeyVariable, jwtSigningKey);

// Single entry point for every client: it issues the demo tokens and proxies /api/* to the services.
builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(catalog)
    .WithReference(ordering)
    .WithReference(inventory)
    .WithEnvironment(JwtSigningKeyVariable, jwtSigningKey)
    .WithExternalHttpEndpoints();

builder.Build().Run();
