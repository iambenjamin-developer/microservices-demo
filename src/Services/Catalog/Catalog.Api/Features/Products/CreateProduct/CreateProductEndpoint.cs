using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using BuildingBlocks.Web.Validation;
using Catalog.Api.Features.Products.GetProductById;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Features.Products.CreateProduct;

internal sealed class CreateProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ProductsApi.Route, async Task<Results<CreatedAtRoute<ProductResponse>, ProblemHttpResult>> (
                CreateProductRequest request,
                CreateProductHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken);

                return result.IsSuccess
                    ? TypedResults.CreatedAtRoute(result.Value, GetProductByIdEndpoint.Name, new { id = result.Value.Id })
                    : result.ToProblem();
            })
            .RequireAdmin()
            .WithRequestValidation<CreateProductRequest>()
            .WithName("CreateProduct")
            .WithSummary("Adds a product to the catalog.")
            .WithTags(ProductsApi.Tag);
}
