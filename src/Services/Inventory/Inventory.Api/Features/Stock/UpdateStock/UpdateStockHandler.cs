using BuildingBlocks.Common.Results;
using Inventory.Api.Domain;
using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Features.Stock.UpdateStock;

internal sealed class UpdateStockHandler(
    InventoryDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<Result<StockResponse>> HandleAsync(string sku, UpdateStockRequest request, CancellationToken cancellationToken)
    {
        var normalizedSku = StockItem.NormalizeSku(sku);

        var stockItem = await dbContext.StockItems.SingleOrDefaultAsync(item => item.Sku == normalizedSku, cancellationToken);
        if (stockItem is null)
        {
            // Stock is only kept for SKUs this service knows; it never creates rows from a URL, so a typo
            // cannot invent a product. TODO(phase 7+): seed new SKUs from a Catalog "product created" event.
            return StockErrors.NotFound(normalizedSku);
        }

        stockItem.SetQuantityAvailable(request.QuantityAvailable, timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);

        return stockItem.ToResponse();
    }
}
