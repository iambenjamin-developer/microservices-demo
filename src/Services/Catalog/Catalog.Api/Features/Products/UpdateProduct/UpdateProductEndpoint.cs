using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using BuildingBlocks.Web.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Features.Products.UpdateProduct;

internal sealed class UpdateProductEndpoint : IEndpoint
{
    // TODO(phase 6): require the Admin role once JWT authentication is in place.
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{ProductsApi.Route}/{{id:guid}}", async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> (
                Guid id,
                UpdateProductRequest request,
                UpdateProductHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
            })
            .WithRequestValidation<UpdateProductRequest>()
            .WithName("UpdateProduct")
            .WithSummary("Updates a product's name, style, presentation and price.")
            .WithTags(ProductsApi.Tag);
}
