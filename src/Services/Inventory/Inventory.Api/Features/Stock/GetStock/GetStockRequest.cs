using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Features.Stock.GetStock;

/// <param name="Skus">Optional filter (<c>?sku=A&amp;sku=B</c>), so a client can check only the SKUs in its cart.</param>
internal sealed record GetStockRequest([FromQuery(Name = "sku")] string[]? Skus);
