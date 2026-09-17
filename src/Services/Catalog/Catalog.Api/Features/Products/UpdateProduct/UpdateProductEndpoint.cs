using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using BuildingBlocks.Web.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Features.Products.UpdateProduct;

internal sealed class UpdateProductEndpoint : IEndpoint
{
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
            .RequireAdmin()
            .WithRequestValidation<UpdateProductRequest>()
            .WithName("UpdateProduct")
            .WithSummary("Updates a product's name, style, presentation and price.")
            .WithTags(ProductsApi.Tag);
}
