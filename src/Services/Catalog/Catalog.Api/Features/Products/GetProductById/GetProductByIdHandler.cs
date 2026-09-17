using BuildingBlocks.Common.Results;
using Catalog.Api.Domain;
using Catalog.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Features.Products.GetProductById;

internal sealed class GetProductByIdHandler(CatalogDbContext dbContext)
{
    public async Task<Result<ProductResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == id)
            .Select(ProductResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return product is null ? ProductErrors.NotFound(id) : product;
    }
}
