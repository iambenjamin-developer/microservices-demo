using BuildingBlocks.Common.Results;
using Catalog.Api.Domain;
using Catalog.Api.Persistence;

namespace Catalog.Api.Features.Products.UpdateProduct;

internal sealed class UpdateProductHandler(CatalogDbContext dbContext)
{
    public async Task<Result<ProductResponse>> HandleAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound(id);
        }

        product.Update(
            request.Name,
            Enum.Parse<BeerStyle>(request.Style, ignoreCase: true),
            request.VolumeMl,
            request.PackSize,
            request.Price);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ProductResponse.FromProduct(product);
    }
}
