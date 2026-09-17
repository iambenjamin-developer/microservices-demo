using BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ordering.Application.Abstractions.Catalog;
using Testcontainers.PostgreSql;

namespace Ordering.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real Ordering API (endpoints, decorators, EF Core, outbox interceptor and processor) against a real
/// PostgreSQL container. Only the edges are faked: the broker (<see cref="IEventBus"/>) and the Catalog service.
/// </summary>
public sealed class OrderingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeEventBus EventBus { get; } = new();

    public FakeCatalogClient CatalogClient { get; } = new();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:orderingdb", _postgres.GetConnectionString());
        // Never used to connect: the event bus is faked and the consumers are disabled below.
        builder.UseSetting("ConnectionStrings:messaging", "Endpoint=sb://localhost.invalid;SharedAccessKeyName=test;SharedAccessKey=test");
        builder.UseSetting("Messaging:Consumers:Enabled", "false");
        builder.UseSetting("Messaging:Outbox:PollingInterval", "00:00:00.100");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(EventBus);

            services.RemoveAll<ICatalogClient>();
            services.AddSingleton<ICatalogClient>(CatalogClient);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class OrderingApiCollection : ICollectionFixture<OrderingApiFactory>
{
    public const string Name = "Ordering API";
}
