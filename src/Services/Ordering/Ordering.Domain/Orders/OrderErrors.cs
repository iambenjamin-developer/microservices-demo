using BuildingBlocks.Common.Results;

namespace Ordering.Domain.Orders;

public static class OrderErrors
{
    public static readonly Error NoItems =
        Error.Validation("Orders.NoItems", "An order must contain at least one item.");

    public static readonly Error TooManyItems =
        Error.Validation("Orders.TooManyItems", $"An order cannot contain more than {Order.MaxItems} different products.");

    public static readonly Error MixedCurrencies =
        Error.Validation("Orders.MixedCurrencies", "All items of an order must be priced in the same currency.");

    public static Error NotFound(Guid orderId) =>
        Error.NotFound("Orders.NotFound", $"The order '{orderId}' was not found.");

    public static Error DuplicateSku(string sku) =>
        Error.Validation("Orders.DuplicateSku", $"The SKU '{sku}' appears more than once; combine it into a single item.");

    public static Error InvalidStatusTransition(Guid orderId, OrderStatus from, OrderStatus to) =>
        Error.Conflict("Orders.InvalidStatusTransition", $"The order '{orderId}' cannot move from {from} to {to}.");
}
