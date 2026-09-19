namespace Inventory.Api.Contracts;

/// <summary>
/// The stock of one SKU. The SKU, not a surrogate id, is the resource key: it is the identity other
/// services already know. <c>QuantityOnHand</c> is available + reserved, i.e. what is physically
/// in the warehouse; the packs in <c>QuantityReserved</c> are already promised to orders.
/// </summary>
public sealed record StockResponse(
    string Sku,
    int QuantityAvailable,
    int QuantityReserved,
    int QuantityOnHand,
    DateTimeOffset UpdatedOnUtc);
