using Inventory.Api.Domain;
using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Features.Stock.GetStock;

internal sealed class GetStockHandler(InventoryDbContext dbContext)
{
    public async Task<IReadOnlyList<StockResponse>> HandleAsync(GetStockRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.StockItems.AsNoTracking();

        if (request.Skus is { Length: > 0 })
        {
            var skus = request.Skus.Select(StockItem.NormalizeSku).Distinct().ToArray();
            query = query.Where(stockItem => skus.Contains(stockItem.Sku));
        }

        // The Mapperly projection becomes the SELECT list, so the read side never materializes entities it
        // does not need.
        return await query
            .OrderBy(stockItem => stockItem.Sku)
            .ProjectToResponse()
            .ToListAsync(cancellationToken);
    }
}
