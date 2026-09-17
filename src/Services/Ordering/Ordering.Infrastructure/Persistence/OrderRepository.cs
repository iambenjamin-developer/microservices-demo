using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Persistence;

internal sealed class OrderRepository(OrderingDbContext dbContext) : IOrderRepository
{
    // The aggregate is always loaded whole (root + items) so its invariants can be checked in memory.
    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    public void Add(Order order) => dbContext.Orders.Add(order);
}
