using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Inventory.Api.Services;

internal static class InventoryServices
{
    /// <summary>
    /// Registered against the interface, scoped like the <c>DbContext</c>: the controller and the <c>OrderPlaced</c>
    /// handler only ever see <see cref="IStockService"/>.
    /// </summary>
    public static IServiceCollection AddInventoryServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        return services.AddScoped<IStockService, StockService>();
    }
}
