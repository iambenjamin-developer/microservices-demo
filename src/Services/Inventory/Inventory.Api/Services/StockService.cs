using BuildingBlocks.Common.Results;
using Inventory.Api.Contracts;
using Inventory.Api.Domain;
using Inventory.Api.Mapping;
using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Services;

/// <summary>
/// The only implementation of <see cref="IStockService"/>. Scoped, like the <see cref="InventoryDbContext"/> it
/// uses: inside a consumed message it shares that context with the consumer pipeline, which is what lets
/// <see cref="StageReservationAsync"/> leave the commit to the pipeline.
/// </summary>
internal sealed class StockService(InventoryDbContext dbContext, TimeProvider timeProvider) : IStockService
{
    public async Task<IReadOnlyList<StockResponse>> GetStockAsync(IReadOnlyCollection<string>? skus, CancellationToken cancellationToken)
    {
        var query = dbContext.StockItems.AsNoTracking();

        if (skus is { Count: > 0 })
        {
            var normalizedSkus = NormalizeSkus(skus);
            query = query.Where(stockItem => normalizedSkus.Contains(stockItem.Sku));
        }

        // The Mapperly projection becomes the SELECT list, so the read side never materializes entities it
        // does not need.
        return await query
            .OrderBy(stockItem => stockItem.Sku)
            .ProjectToResponse()
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<StockResponse>> UpdateQuantityAvailableAsync(string sku, UpdateStockRequest request, CancellationToken cancellationToken)
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

    public async Task<ReservationOutcome> StageReservationAsync(IReadOnlyCollection<ReservationLine> lines, CancellationToken cancellationToken)
    {
        var skus = NormalizeSkus(lines.Select(line => line.Sku));

        // Tracked (not AsNoTracking): these rows are the ones the caller will save.
        var stockItems = await dbContext.StockItems
            .Where(stockItem => skus.Contains(stockItem.Sku))
            .ToListAsync(cancellationToken);

        return StockReservation.Reserve(stockItems, lines, timeProvider.GetUtcNow());
    }

    private static string[] NormalizeSkus(IEnumerable<string> skus) =>
        [.. skus.Select(StockItem.NormalizeSku).Distinct()];
}
