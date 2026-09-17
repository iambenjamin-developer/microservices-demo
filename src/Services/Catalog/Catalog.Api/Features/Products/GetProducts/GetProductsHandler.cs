using Catalog.Api.Domain;
using Catalog.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Features.Products.GetProducts;

internal sealed class GetProductsHandler(CatalogDbContext dbContext)
{
    public async Task<IReadOnlyList<ProductResponse>> HandleAsync(GetProductsRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.Products.AsNoTracking();

        if (request.Skus is { Length: > 0 })
        {
            var skus = request.Skus.Select(Product.NormalizeSku).Distinct().ToArray();
            query = query.Where(product => skus.Contains(product.Sku));
        }

        return await query
            .OrderBy(product => product.Name)
            .Select(ProductResponse.Projection)
            .ToListAsync(cancellationToken);
    }
}
