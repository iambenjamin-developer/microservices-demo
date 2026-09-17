using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Api.Customers;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Orders;
using Ordering.Application.Orders.GetOrders;

namespace Ordering.Api.Endpoints.Orders;

internal sealed class GetOrdersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(OrdersApi.Route, async Task<Results<Ok<IReadOnlyList<OrderSummaryResponse>>, ProblemHttpResult>> (
                CurrentCustomer customer,
                IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummaryResponse>> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetOrdersQuery(customer.Id), cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
            })
            .WithName("GetOrders")
            .WithSummary("Lists the customer's orders, most recent first.")
            .WithTags(OrdersApi.Tag);
}
