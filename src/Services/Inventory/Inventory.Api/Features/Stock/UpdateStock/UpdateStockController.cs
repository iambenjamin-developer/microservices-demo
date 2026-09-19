using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Features.Stock.UpdateStock;

[Route(StockApi.Route)]
[Tags(StockApi.Tag)]
public sealed class UpdateStockController : ApiControllerBase
{
    [HttpPut("{sku}")]
    [RequireAdmin]
    [EndpointName("UpdateStock")]
    [EndpointSummary("Sets how many packs of a SKU are available for sale.")]
    [ProducesResponseType<StockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<StockResponse>> UpdateStock(
        string sku,
        UpdateStockRequest request,
        [FromServices] UpdateStockHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(sku, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
