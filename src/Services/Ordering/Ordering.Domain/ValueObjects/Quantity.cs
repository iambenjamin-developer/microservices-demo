using BuildingBlocks.Common.Results;

namespace Ordering.Domain.ValueObjects;

/// <summary>Number of packs of a product in an order line.</summary>
public sealed record Quantity
{
    public const int Max = 1000;

    private Quantity(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static Result<Quantity> Create(int value) =>
        value is >= 1 and <= Max
            ? new Quantity(value)
            : QuantityErrors.OutOfRange(value);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
