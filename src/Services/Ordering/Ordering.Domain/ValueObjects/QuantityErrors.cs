using BuildingBlocks.Common.Results;

namespace Ordering.Domain.ValueObjects;

public static class QuantityErrors
{
    public static Error OutOfRange(int value) =>
        Error.Validation("Quantity.OutOfRange", $"The quantity must be between 1 and {Quantity.Max}, but was {value}.");
}
