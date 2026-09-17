using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Persistence;

/// <summary>
/// Opening stock for the demo catalog, plugged into EF Core <c>UseSeeding</c>/<c>UseAsyncSeeding</c>:
/// it runs right after migrations are applied and only inserts into an empty table.
/// </summary>
/// <remarks>
/// The SKUs mirror <c>CatalogSeeder</c>. They are duplicated on purpose: Inventory owns its own database and
/// must not reference Catalog. "MIDNIGHT-STOUT-500" is deliberately short so the demo can show a rejected order.
/// </remarks>
public static class InventorySeeder
{
    public static DbContextOptionsBuilder UseInventorySeeding(this DbContextOptionsBuilder options) =>
        options
            .UseSeeding((context, _) =>
            {
                var stockItems = context.Set<StockItem>();
                if (!stockItems.Any())
                {
                    stockItems.AddRange(CreateStockItems(TimeProvider.System.GetUtcNow()));
                    context.SaveChanges();
                }
            })
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                var stockItems = context.Set<StockItem>();
                if (!await stockItems.AnyAsync(cancellationToken))
                {
                    stockItems.AddRange(CreateStockItems(TimeProvider.System.GetUtcNow()));
                    await context.SaveChangesAsync(cancellationToken);
                }
            });

    public static IReadOnlyList<StockItem> CreateStockItems(DateTimeOffset utcNow) =>
    [
        StockItem.Create("DUFF-STYLE-CLASSIC-350", 400, utcNow),
        StockItem.Create("GOLDEN-LAGER-350", 500, utcNow),
        StockItem.Create("GOLDEN-LAGER-1000", 250, utcNow),
        StockItem.Create("CRISP-PILSNER-269", 300, utcNow),
        StockItem.Create("SUNNY-WHEAT-330", 180, utcNow),
        StockItem.Create("AMBER-ALE-600", 220, utcNow),
        StockItem.Create("HOPPY-TRAIL-IPA-473", 120, utcNow),
        StockItem.Create("MIDNIGHT-STOUT-500", 5, utcNow),
    ];
}
