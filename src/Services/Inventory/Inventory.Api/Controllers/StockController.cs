using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Mvc;
using Inventory.Api.Contracts;
using Inventory.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>
/// The HTTP surface of Inventory: it translates HTTP into <see cref="IStockService"/> calls and results back into
/// HTTP, and nothing else. It never sees the <c>DbContext</c>; request validation runs before the action (global
/// FluentValidation filter).
/// </summary>
[Route("/api/stock")]
[Tags("Stock")]
public sealed class StockController(IStockService stockService) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("GetStock")]
    [EndpointSummary("Lists the stock of every SKU, optionally filtered by SKU.")]
    [ProducesResponseType<IReadOnlyList<StockResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<StockResponse>>> GetStock(
        [FromQuery] GetStockRequest request,
        CancellationToken cancellationToken)
    {
        var stock = await stockService.GetStockAsync(request.Skus, cancellationToken);
        return Ok(stock);
    }

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
        CancellationToken cancellationToken)
    {
        var result = await stockService.UpdateQuantityAvailableAsync(sku, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
