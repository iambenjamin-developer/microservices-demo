using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Stock.GetStock;

internal sealed class GetStockEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(StockApi.Route, async Task<Ok<IReadOnlyList<StockResponse>>> (
                [AsParameters] GetStockRequest request,
                GetStockHandler handler,
                CancellationToken cancellationToken) =>
            {
                var stock = await handler.HandleAsync(request, cancellationToken);
                return TypedResults.Ok(stock);
            })
            .WithRequestValidation<GetStockRequest>()
            .WithName("GetStock")
            .WithSummary("Lists the stock of every SKU, optionally filtered by SKU.")
            .WithTags(StockApi.Tag);
}
