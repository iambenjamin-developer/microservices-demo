namespace Ordering.Domain.Orders;

/// <summary>
/// Collection-like access to <see cref="Order"/> aggregates for the write side. It loads and adds whole
/// aggregates only; API reads use projections instead. Changes are committed by the unit of work.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    void Add(Order order);
}
