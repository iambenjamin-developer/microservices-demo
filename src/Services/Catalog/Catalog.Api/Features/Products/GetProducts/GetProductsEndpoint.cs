using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Features.Products.GetProducts;

internal sealed class GetProductsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProductsApi.Route, async Task<Ok<IReadOnlyList<ProductResponse>>> (
                [AsParameters] GetProductsRequest request,
                GetProductsHandler handler,
                CancellationToken cancellationToken) =>
            {
                var products = await handler.HandleAsync(request, cancellationToken);
                return TypedResults.Ok(products);
            })
            .WithRequestValidation<GetProductsRequest>()
            .WithName("GetProducts")
            .WithSummary("Lists the catalog, optionally filtered by SKU.")
            .WithTags(ProductsApi.Tag);
}
