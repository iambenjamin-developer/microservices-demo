using BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Inventory.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real Inventory API (controllers, validation, authentication, EF Core, migrations, seeding, the
/// <c>OrderPlaced</c> handler and the outbox processor) against a real PostgreSQL container. Only the broker is faked.
/// </summary>
public sealed class InventoryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeEventBus EventBus { get; } = new();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:inventorydb", _postgres.GetConnectionString());
        // Never used to connect: the event bus is faked and the consumers are disabled below.
        builder.UseSetting("ConnectionStrings:messaging", "Endpoint=sb://localhost.invalid;SharedAccessKeyName=test;SharedAccessKey=test");
        builder.UseSetting("Messaging:Consumers:Enabled", "false");
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(EventBus);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class InventoryApiCollection : ICollectionFixture<InventoryApiFactory>
{
    public const string Name = "Inventory API";
}
