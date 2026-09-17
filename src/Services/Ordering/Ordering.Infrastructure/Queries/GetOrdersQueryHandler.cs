using BuildingBlocks.Common.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Orders;
using Ordering.Application.Orders.GetOrders;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure.Queries;

internal sealed class GetOrdersQueryHandler(
    OrderingDbContext dbContext,
    TypeAdapterConfig mappingConfig) : IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummaryResponse>>
{
    /// <summary>Upper bound for the list; paging is out of scope for the MVP.</summary>
    public const int MaxOrders = 100;

    public async Task<Result<IReadOnlyList<OrderSummaryResponse>>> HandleAsync(GetOrdersQuery query, CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == query.CustomerId)
            .OrderByDescending(order => order.PlacedOnUtc)
            .Take(MaxOrders)
            .ProjectToType<OrderSummaryResponse>(mappingConfig)
            .ToListAsync(cancellationToken);

        return orders;
    }
}
