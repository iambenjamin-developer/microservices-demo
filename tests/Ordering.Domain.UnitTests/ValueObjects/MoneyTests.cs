using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Create_ValidAmountAndLowercaseCurrency_ReturnsNormalizedMoney()
    {
        var result = Money.Create(12.50m, " usd ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(12.50m);
        result.Value.Currency.ShouldBe("USD");
    }

    [Fact]
    public void Create_NegativeAmount_ReturnsNegativeAmountError()
    {
        var result = Money.Create(-0.01m, "USD");

        result.Error.ShouldBe(MoneyErrors.NegativeAmount);
    }

    [Fact]
    public void Create_MoreThanTwoDecimalPlaces_ReturnsInvalidScaleError()
    {
        var result = Money.Create(1.005m, "USD");

        result.Error.ShouldBe(MoneyErrors.InvalidScale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData("U5D")]
    public void Create_InvalidCurrency_ReturnsInvalidCurrencyError(string? currency)
    {
        var result = Money.Create(10m, currency!);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Money.InvalidCurrency");
    }

    [Fact]
    public void Equals_SameAmountAndCurrency_AreEqual()
    {
        Money.Create(10m, "USD").Value.ShouldBe(Money.Create(10.00m, "usd").Value);
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSum()
    {
        var sum = Usd(10.25m).Add(Usd(4.75m));

        sum.ShouldBe(Usd(15m));
    }

    [Fact]
    public void Add_DifferentCurrency_ThrowsInvalidOperationException()
    {
        var eur = Money.Create(1m, "EUR").Value;

        Should.Throw<InvalidOperationException>(() => Usd(1m).Add(eur));
    }

    [Fact]
    public void Subtract_ResultWouldBeNegative_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() => Usd(1m).Subtract(Usd(2m)));
    }

    [Fact]
    public void Multiply_PositiveFactor_ReturnsProduct()
    {
        Usd(12.40m).Multiply(3).ShouldBe(Usd(37.20m));
    }

    [Theory]
    [InlineData(0.25, 0.1, 0.03)]
    [InlineData(100, 0.05, 5)]
    [InlineData(33.33, 0.1, 3.33)]
    public void ApplyRate_AnyRate_RoundsHalfAwayFromZeroToCents(decimal amount, decimal rate, decimal expected)
    {
        Usd(amount).ApplyRate(rate).Amount.ShouldBe(expected);
    }

    [Fact]
    public void Zero_InvalidCurrency_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Money.Zero("DOLLARS"));
    }

    private static Money Usd(decimal amount) => Money.Create(amount, "USD").Value;
}
