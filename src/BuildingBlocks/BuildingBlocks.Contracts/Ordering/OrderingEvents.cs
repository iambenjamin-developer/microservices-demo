namespace BuildingBlocks.Contracts.Ordering;

public sealed record OrderPlaced(
    Guid OrderId,
    string CustomerId,
    IReadOnlyList<OrderPlacedItem> Items) : IntegrationEvent;

public sealed record OrderPlacedItem(string Sku, int Quantity);

public sealed record OrderConfirmed(
    Guid OrderId,
    string CustomerId,
    string CustomerEmail,
    decimal Total,
    string Currency) : IntegrationEvent;

public sealed record OrderRejected(
    Guid OrderId,
    string CustomerId,
    string CustomerEmail,
    string Reason) : IntegrationEvent;
