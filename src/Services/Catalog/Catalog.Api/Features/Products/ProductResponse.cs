using System.Linq.Expressions;
using Catalog.Api.Domain;

namespace Catalog.Api.Features.Products;

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    BeerStyle Style,
    int VolumeMl,
    int PackSize,
    decimal Price)
{
    /// <summary>Translated to SQL by EF Core, so read queries only select the columns they return.</summary>
    public static readonly Expression<Func<Product, ProductResponse>> Projection = product =>
        new ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Style,
            product.VolumeMl,
            product.PackSize,
            product.Price);

    private static readonly Func<Product, ProductResponse> _fromProduct = Projection.Compile();

    public static ProductResponse FromProduct(Product product) => _fromProduct(product);
}
