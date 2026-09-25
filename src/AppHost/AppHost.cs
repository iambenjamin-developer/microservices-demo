using AppHost;

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
var notificationsDb = postgres.AddDatabase("notificationsdb");

// Azure Service Bus: the local emulator by default, or a real namespace that Aspire provisions when
// Messaging:Broker is "Azure" (launch profile "https-azure"). The topology is the same in both.
var serviceBus = builder.AddMessaging(ContainerPrefix);

// Mail catcher: Mailpit speaks real SMTP and shows every message it receives in a web UI, so the demo can show
// an e-mail leaving the system without anybody owning a mail account. Swapping Email:Host/Port for Gmail is enough
// to deliver for real (see .env.example).
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.31")
    .WithContainerName($"{ContainerPrefix}mailpit")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpoint(name: "smtp", port: 1025, targetPort: 1025)
    .WithHttpEndpoint(name: "ui", port: 8025, targetPort: 8025)
    .WithUrlForEndpoint("ui", url => url.DisplayText = "Inbox");

var mailpitSmtp = mailpit.GetEndpoint("smtp");

// The Azure Functions runtime keeps its own bookkeeping (host id lease, trigger state) in a storage account.
// Azurite emulates it locally; it is infrastructure for the Functions host, not application data.
var functionsStorage = builder.AddAzureStorage("functions-storage")
    .RunAsEmulator(emulator => emulator
        .WithContainerName($"{ContainerPrefix}azurite")
        .WithLifetime(ContainerLifetime.Persistent));

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

// Notifications is an Azure Function (isolated worker): a Service Bus trigger records the order outcome and
// e-mails it, and an HTTP trigger serves the panel. It is the end of the choreography — it publishes nothing.
var notifications = builder.AddAzureFunctionsProject<Projects.Notifications>("notifications")
    .WithHostStorage(functionsStorage)
    .WithReference(notificationsDb)
    .WaitFor(notificationsDb)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WaitFor(mailpit)
    .WithEnvironment("Email__Host", mailpitSmtp.Property(EndpointProperty.Host))
    .WithEnvironment("Email__Port", mailpitSmtp.Property(EndpointProperty.Port))
    .WithEnvironment(JwtSigningKeyVariable, jwtSigningKey);

// Single entry point for every client: it issues the demo tokens and proxies /api/* to the services.
var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(catalog)
    .WithReference(ordering)
    .WithReference(inventory)
    .WithReference(notifications)
    .WithEnvironment(JwtSigningKeyVariable, jwtSigningKey)
    .WithExternalHttpEndpoints();

// The React app. Everything it needs is behind the Gateway, so that is the only address it is given:
// a browser cannot use service discovery, and Vite only exposes variables prefixed with VITE_.
// The dev server port is fixed because the Gateway allows a fixed list of CORS origins.
builder.AddViteApp("web", "../Web")
    .WithNpm()
    .WithEnvironment("VITE_GATEWAY_URL", gateway.GetEndpoint("http"))
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5173;
        endpoint.TargetPort = 5173;
        endpoint.IsProxied = false;
    })
    .WithExternalHttpEndpoints();

builder.Build().Run();
