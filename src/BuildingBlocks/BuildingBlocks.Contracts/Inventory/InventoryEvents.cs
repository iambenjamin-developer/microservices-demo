namespace BuildingBlocks.Contracts.Inventory;

public sealed record StockReserved(Guid OrderId) : IntegrationEvent;

public sealed record StockRejected(
    Guid OrderId,
    string Reason,
    IReadOnlyList<string> UnavailableSkus) : IntegrationEvent;
