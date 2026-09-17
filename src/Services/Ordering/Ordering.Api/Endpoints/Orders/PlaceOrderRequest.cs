namespace Ordering.Api.Endpoints.Orders;

/// <summary>
/// Body of <c>POST /api/orders</c>: SKUs and quantities (packs) only. Prices come from Catalog and the customer
/// from the caller's identity, so neither can be forged by the client.
/// </summary>
public sealed record PlaceOrderRequest(IReadOnlyList<PlaceOrderRequestItem>? Items);

public sealed record PlaceOrderRequestItem(string Sku, int Quantity);
