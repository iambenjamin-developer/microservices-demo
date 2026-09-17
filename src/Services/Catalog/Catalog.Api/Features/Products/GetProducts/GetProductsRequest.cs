using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Features.Products.GetProducts;

/// <param name="Skus">Optional filter (<c>?sku=A&amp;sku=B</c>) used by Ordering to snapshot prices in one call.</param>
internal sealed record GetProductsRequest([FromQuery(Name = "sku")] string[]? Skus);
