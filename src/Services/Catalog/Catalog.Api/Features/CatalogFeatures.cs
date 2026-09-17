using Catalog.Api.Features.Products.CreateProduct;
using Catalog.Api.Features.Products.GetProductById;
using Catalog.Api.Features.Products.GetProducts;
using Catalog.Api.Features.Products.UpdateProduct;

namespace Catalog.Api.Features;

internal static class CatalogFeatures
{
    /// <summary>One handler per slice, registered explicitly: the list reads as the service's feature index.</summary>
    public static IServiceCollection AddCatalogFeatures(this IServiceCollection services) =>
        services
            .AddScoped<GetProductsHandler>()
            .AddScoped<GetProductByIdHandler>()
            .AddScoped<CreateProductHandler>()
            .AddScoped<UpdateProductHandler>();
}
