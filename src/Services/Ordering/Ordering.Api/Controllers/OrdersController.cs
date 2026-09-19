using BuildingBlocks.Web.Mvc;
using Microsoft.AspNetCore.Mvc;
using Ordering.Api.Customers;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Orders;
using Ordering.Application.Orders.GetOrderById;
using Ordering.Application.Orders.GetOrders;
using Ordering.Application.Orders.PlaceOrder;

namespace Ordering.Api.Controllers;

/// <summary>
/// The HTTP surface of Ordering: it translates HTTP into commands and queries and results back into HTTP, and
/// nothing else. Each action takes only the handler it needs (<c>[FromServices]</c>), so a request does not build
/// the dependencies of the others.
/// </summary>
[Route("/api/orders")]
[Tags("Orders")]
public sealed class OrdersController : ApiControllerBase
{
    // 202 Accepted: the order is stored as Pending; confirmation happens asynchronously after Inventory answers.
    // The Location header points to the order, which the client polls to follow its status.
    [HttpPost]
    [EndpointName("PlaceOrder")]
    [EndpointSummary("Places an order with prices snapshotted from Catalog; it stays Pending until stock is reserved.")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<OrderResponse>> PlaceOrder(
        PlaceOrderRequest request,
        CurrentCustomer customer,
        [FromServices] ICommandHandler<PlaceOrderCommand, OrderResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new PlaceOrderCommand(
            customer.Id,
            customer.Email,
            [.. (request.Items ?? []).Select(item => new PlaceOrderItem(item.Sku, item.Quantity))]);

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? AcceptedAtAction(nameof(GetOrderById), new { id = result.Value.Id }, result.Value)
            : Problem(result.Error);
    }

    [HttpGet]
    [EndpointName("GetOrders")]
    [EndpointSummary("Lists the customer's orders, most recent first.")]
    [ProducesResponseType<IReadOnlyList<OrderSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryResponse>>> GetOrders(
        CurrentCustomer customer,
        [FromServices] IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummaryResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetOrdersQuery(customer.Id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("{id:guid}")]
    [EndpointName("GetOrderById")]
    [EndpointSummary("Gets one of the customer's orders, with its items and current status.")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OrderResponse>> GetOrderById(
        Guid id,
        CurrentCustomer customer,
        [FromServices] IQueryHandler<GetOrderByIdQuery, OrderResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetOrderByIdQuery(id, customer.Id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
