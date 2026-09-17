namespace Catalog.Api.Features.Products.UpdateProduct;

/// <summary>
/// <c>Style</c> is a <see cref="Domain.BeerStyle"/> name (case-insensitive); <c>Price</c> is per pack.
/// The SKU is not part of the request: it is the cross-service identity and is immutable.
/// </summary>
public sealed record UpdateProductRequest(
    string Name,
    string Style,
    int VolumeMl,
    int PackSize,
    decimal Price);
