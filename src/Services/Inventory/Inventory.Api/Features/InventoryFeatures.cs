using Inventory.Api.Features.Stock.GetStock;
using Inventory.Api.Features.Stock.UpdateStock;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Inventory.Api.Features;

internal static class InventoryFeatures
{
    /// <summary>One handler per slice, registered explicitly: the list reads as the service's feature index.</summary>
    public static IServiceCollection AddInventoryFeatures(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        return services
            .AddScoped<GetStockHandler>()
            .AddScoped<UpdateStockHandler>();
    }
}
