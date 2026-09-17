namespace Inventory.Api.Domain;

/// <summary>One line of a reservation request: how many packs of a SKU an order needs.</summary>
public sealed record ReservationLine(string Sku, int Quantity);
