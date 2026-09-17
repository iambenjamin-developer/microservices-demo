using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.UnitTests.ValueObjects;

public sealed class SkuTests
{
    [Fact]
    public void Create_LowercaseWithSpaces_ReturnsTrimmedUppercaseSku()
    {
        var result = Sku.Create("  golden-lager-350 ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("GOLDEN-LAGER-350");
    }

    [Fact]
    public void Equals_DifferentCasing_AreEqual()
    {
        Sku.Create("amber-ale-600").Value.ShouldBe(Sku.Create("AMBER-ALE-600").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Empty_ReturnsEmptyError(string? value)
    {
        Sku.Create(value!).Error.ShouldBe(SkuErrors.Empty);
    }

    [Fact]
    public void Create_LongerThanMaxLength_ReturnsTooLongError()
    {
        Sku.Create(new string('A', Sku.MaxLength + 1)).Error.ShouldBe(SkuErrors.TooLong);
    }

    [Theory]
    [InlineData("GOLDEN LAGER")]
    [InlineData("-GOLDEN")]
    [InlineData("GOLDEN-")]
    [InlineData("GOLDEN--LAGER")]
    [InlineData("GOLDEN_LAGER")]
    public void Create_InvalidFormat_ReturnsInvalidFormatError(string value)
    {
        Sku.Create(value).Error.Code.ShouldBe("Sku.InvalidFormat");
    }
}
