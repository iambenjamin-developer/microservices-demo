using BuildingBlocks.Common.Results;

namespace Catalog.Api.Domain;

public static class ProductErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Products.NotFound", $"The product '{id}' was not found.");

    public static Error SkuAlreadyExists(string sku) =>
        Error.Conflict("Products.SkuAlreadyExists", $"A product with SKU '{sku}' already exists.");
}
