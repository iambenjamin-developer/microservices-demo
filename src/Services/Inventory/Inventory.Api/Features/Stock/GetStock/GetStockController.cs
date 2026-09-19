using BuildingBlocks.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Features.Stock.GetStock;

/// <summary>One controller per slice keeps the vertical slice layout: the feature folder still holds all of it.</summary>
[Route(StockApi.Route)]
[Tags(StockApi.Tag)]
public sealed class GetStockController : ApiControllerBase
{
    [HttpGet]
    [EndpointName("GetStock")]
    [EndpointSummary("Lists the stock of every SKU, optionally filtered by SKU.")]
    [ProducesResponseType<IReadOnlyList<StockResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<StockResponse>>> GetStock(
        [FromQuery] GetStockRequest request,
        [FromServices] GetStockHandler handler,
        CancellationToken cancellationToken)
    {
        var stock = await handler.HandleAsync(request, cancellationToken);
        return Ok(stock);
    }
}
