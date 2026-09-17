namespace Ordering.Domain.Orders;

public enum OrderStatus
{
    /// <summary>Placed and waiting for Inventory to reserve stock.</summary>
    Pending,

    /// <summary>Stock was reserved. Terminal state.</summary>
    Confirmed,

    /// <summary>Stock could not be reserved. Terminal state.</summary>
    Rejected,
}
