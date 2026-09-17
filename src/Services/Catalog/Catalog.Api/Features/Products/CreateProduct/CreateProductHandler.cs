using BuildingBlocks.Common.Results;
using Catalog.Api.Domain;
using Catalog.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Catalog.Api.Features.Products.CreateProduct;

internal sealed class CreateProductHandler(CatalogDbContext dbContext)
{
    public async Task<Result<ProductResponse>> HandleAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var sku = Product.NormalizeSku(request.Sku);

        if (await dbContext.Products.AnyAsync(product => product.Sku == sku, cancellationToken))
        {
            return ProductErrors.SkuAlreadyExists(sku);
        }

        var product = Product.Create(
            sku,
            request.Name,
            Enum.Parse<BeerStyle>(request.Style, ignoreCase: true),
            request.VolumeMl,
            request.PackSize,
            request.Price);

        dbContext.Products.Add(product);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two concurrent requests passed the check above; the unique index decides.
            return ProductErrors.SkuAlreadyExists(sku);
        }

        return ProductResponse.FromProduct(product);
    }
}
