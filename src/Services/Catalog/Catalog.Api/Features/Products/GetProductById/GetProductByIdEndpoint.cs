using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Features.Products.GetProductById;

internal sealed class GetProductByIdEndpoint : IEndpoint
{
    public const string Name = "GetProductById";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{ProductsApi.Route}/{{id:guid}}", async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> (
                Guid id,
                GetProductByIdHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
            })
            .WithName(Name)
            .WithSummary("Gets a product by id.")
            .WithTags(ProductsApi.Tag);
}
