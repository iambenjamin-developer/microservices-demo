using BuildingBlocks.Common.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Orders;
using Ordering.Application.Orders.GetOrderById;
using Ordering.Domain.Orders;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure.Queries;

/// <summary>Read side: no tracking, no aggregate; Mapster turns the mapping into the SQL SELECT list.</summary>
internal sealed class GetOrderByIdQueryHandler(
    OrderingDbContext dbContext,
    TypeAdapterConfig mappingConfig) : IQueryHandler<GetOrderByIdQuery, OrderResponse>
{
    public async Task<Result<OrderResponse>> HandleAsync(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Id == query.OrderId && order.CustomerId == query.CustomerId)
            .ProjectToType<OrderResponse>(mappingConfig)
            .SingleOrDefaultAsync(cancellationToken);

        return order is null ? OrderErrors.NotFound(query.OrderId) : order;
    }
}
