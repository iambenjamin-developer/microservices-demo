using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Contracts;

/// <param name="Skus">Optional filter (<c>?sku=A&amp;sku=B</c>), so a client can check only the SKUs in its cart.</param>
/// <remarks>
/// The attribute is repeated on the property: MVC binds through the constructor parameter, but ApiExplorer (and so
/// the OpenAPI document) reads the property.
/// </remarks>
public sealed record GetStockRequest([FromQuery(Name = "sku")][property: FromQuery(Name = "sku")] string[]? Skus);
