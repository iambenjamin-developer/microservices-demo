using Mapster;
using MapsterMapper;

namespace Inventory.Api.Mapping;

public static class InventoryMapping
{
    /// <summary>
    /// Builds the Mapster configuration from every <see cref="IRegister"/> in this assembly and exposes it as
    /// <see cref="IMapper"/> (in-memory maps) and <see cref="TypeAdapterConfig"/> (<c>ProjectToType</c> in queries).
    /// </summary>
    /// <remarks>
    /// Strict on purpose: only declared type pairs can be mapped and every destination member needs a source, so a
    /// renamed property breaks <c>Compile()</c> in a unit test instead of silently returning a default value.
    /// </remarks>
    public static TypeAdapterConfig CreateMappingConfig()
    {
        var config = new TypeAdapterConfig
        {
            RequireExplicitMapping = true,
            RequireDestinationMemberSource = true,
        };

        config.Scan(typeof(InventoryMapping).Assembly);
        return config;
    }

    public static IServiceCollection AddInventoryMapping(this IServiceCollection services) =>
        services
            .AddSingleton(CreateMappingConfig())
            .AddScoped<IMapper, ServiceMapper>();
}
