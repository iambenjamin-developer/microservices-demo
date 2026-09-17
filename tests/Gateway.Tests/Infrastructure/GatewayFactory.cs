using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gateway.Tests.Infrastructure;

/// <summary>
/// Hosts the real Gateway in memory. No backend service is started: these tests cover what the Gateway
/// decides on its own — issuing tokens and rejecting requests before they are ever proxied.
/// </summary>
public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "gateway-tests-signing-key-with-enough-entropy";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
    }
}

[CollectionDefinition(Name)]
public sealed class GatewayCollection : ICollectionFixture<GatewayFactory>
{
    public const string Name = "Gateway";
}
