namespace Inventory.Api.Features.Stock.UpdateStock;

/// <summary>
/// Replenishment: sets how many packs are available for sale. Reserved packs are not part of the request —
/// they belong to orders that were already accepted.
/// </summary>
public sealed record UpdateStockRequest(int QuantityAvailable);
