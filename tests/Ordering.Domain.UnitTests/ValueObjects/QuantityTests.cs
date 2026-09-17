using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.UnitTests.ValueObjects;

public sealed class QuantityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(Quantity.Max)]
    public void Create_WithinRange_ReturnsQuantity(int value)
    {
        Quantity.Create(value).Value.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(Quantity.Max + 1)]
    public void Create_OutOfRange_ReturnsOutOfRangeError(int value)
    {
        Quantity.Create(value).Error.Code.ShouldBe("Quantity.OutOfRange");
    }
}
