namespace Catalog.Api.Features.Products.CreateProduct;

/// <summary>
/// <c>Style</c> is a <see cref="Domain.BeerStyle"/> name (case-insensitive); <c>Price</c> is per pack.
/// </summary>
public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string Style,
    int VolumeMl,
    int PackSize,
    decimal Price);
