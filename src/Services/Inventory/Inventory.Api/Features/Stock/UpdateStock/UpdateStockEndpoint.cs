using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using BuildingBlocks.Web.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Stock.UpdateStock;

internal sealed class UpdateStockEndpoint : IEndpoint
{
    // TODO(phase 6): require the Admin role once JWT authentication is in place.
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{StockApi.Route}/{{sku}}", async Task<Results<Ok<StockResponse>, ProblemHttpResult>> (
                string sku,
                UpdateStockRequest request,
                UpdateStockHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(sku, request, cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
            })
            .WithRequestValidation<UpdateStockRequest>()
            .WithName("UpdateStock")
            .WithSummary("Sets how many packs of a SKU are available for sale.")
            .WithTags(StockApi.Tag);
}
