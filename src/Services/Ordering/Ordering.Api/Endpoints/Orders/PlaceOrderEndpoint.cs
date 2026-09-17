using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Api.Customers;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Orders;
using Ordering.Application.Orders.PlaceOrder;

namespace Ordering.Api.Endpoints.Orders;

internal sealed class PlaceOrderEndpoint : IEndpoint
{
    // 202 Accepted: the order is stored as Pending; confirmation happens asynchronously after Inventory answers.
    // The Location header points to the order, which the client polls to follow its status.
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(OrdersApi.Route, async Task<Results<AcceptedAtRoute<OrderResponse>, ProblemHttpResult>> (
                PlaceOrderRequest request,
                CurrentCustomer customer,
                ICommandHandler<PlaceOrderCommand, OrderResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new PlaceOrderCommand(
                    customer.Id,
                    customer.Email,
                    [.. (request.Items ?? []).Select(item => new PlaceOrderItem(item.Sku, item.Quantity))]);

                var result = await handler.HandleAsync(command, cancellationToken);

                return result.IsSuccess
                    ? TypedResults.AcceptedAtRoute(result.Value, GetOrderByIdEndpoint.Name, new { id = result.Value.Id })
                    : result.ToProblem();
            })
            .WithName("PlaceOrder")
            .WithSummary("Places an order with prices snapshotted from Catalog; it stays Pending until stock is reserved.")
            .WithTags(OrdersApi.Tag)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
