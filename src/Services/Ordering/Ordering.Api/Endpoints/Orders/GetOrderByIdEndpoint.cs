using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Api.Customers;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Orders;
using Ordering.Application.Orders.GetOrderById;

namespace Ordering.Api.Endpoints.Orders;

internal sealed class GetOrderByIdEndpoint : IEndpoint
{
    public const string Name = "GetOrderById";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{OrdersApi.Route}/{{id:guid}}", async Task<Results<Ok<OrderResponse>, ProblemHttpResult>> (
                Guid id,
                CurrentCustomer customer,
                IQueryHandler<GetOrderByIdQuery, OrderResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetOrderByIdQuery(id, customer.Id), cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
            })
            .WithName(Name)
            .WithSummary("Gets one of the customer's orders, with its items and current status.")
            .WithTags(OrdersApi.Tag);
}
