using BuildingBlocks.Common.Results;

namespace Ordering.Application.Orders.PlaceOrder;

public static class PlaceOrderErrors
{
    public static Error UnknownProducts(IEnumerable<string> skus) =>
        Error.Validation("Orders.UnknownProducts", $"These SKUs do not exist in the catalog: {string.Join(", ", skus)}.");
}
