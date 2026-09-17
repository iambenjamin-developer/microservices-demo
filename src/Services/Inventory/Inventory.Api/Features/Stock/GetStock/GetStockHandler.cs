using Inventory.Api.Domain;
using Inventory.Api.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Features.Stock.GetStock;

internal sealed class GetStockHandler(InventoryDbContext dbContext, TypeAdapterConfig mappingConfig)
{
    public async Task<IReadOnlyList<StockResponse>> HandleAsync(GetStockRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.StockItems.AsNoTracking();

        if (request.Skus is { Length: > 0 })
        {
            var skus = request.Skus.Select(StockItem.NormalizeSku).Distinct().ToArray();
            query = query.Where(stockItem => skus.Contains(stockItem.Sku));
        }

        // ProjectToType turns the Mapster configuration into the SELECT list, so the read side never
        // materializes entities it does not need.
        return await query
            .OrderBy(stockItem => stockItem.Sku)
            .ProjectToType<StockResponse>(mappingConfig)
            .ToListAsync(cancellationToken);
    }
}
