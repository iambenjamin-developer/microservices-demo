using BuildingBlocks.Common.Results;

namespace Inventory.Api.Domain;

public static class StockErrors
{
    public static Error NotFound(string sku) =>
        Error.NotFound("Stock.NotFound", $"No stock is kept for SKU '{sku}'.");
}
