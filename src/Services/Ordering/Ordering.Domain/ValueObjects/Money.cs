using BuildingBlocks.Common.Results;

namespace Ordering.Domain.ValueObjects;

/// <summary>
/// A non-negative amount in a given currency. Immutable and compared by value; arithmetic between
/// different currencies is a programming error, so it throws instead of returning a <see cref="Result"/>.
/// </summary>
public sealed record Money
{
    public const int CurrencyLength = 3;
    public const int Scale = 2;

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0)
        {
            return MoneyErrors.NegativeAmount;
        }

        if (decimal.Round(amount, Scale) != amount)
        {
            return MoneyErrors.InvalidScale;
        }

        var normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedCurrency.Length != CurrencyLength || !normalizedCurrency.All(char.IsAsciiLetterUpper))
        {
            return MoneyErrors.InvalidCurrency(currency);
        }

        return new Money(amount, normalizedCurrency);
    }

    public static Money Zero(string currency)
    {
        var result = Create(0, currency);
        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Description, nameof(currency));
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        if (other.Amount > Amount)
        {
            throw new InvalidOperationException($"Subtracting {other} from {this} would produce a negative amount.");
        }

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(int factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);
        return new Money(Amount * factor, Currency);
    }

    /// <summary>Applies a rate (e.g. <c>0.05m</c> for 5%) rounding half away from zero to cents.</summary>
    public Money ApplyRate(decimal rate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rate);
        return new Money(decimal.Round(Amount * rate, Scale, MidpointRounding.AwayFromZero), Currency);
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException($"Cannot combine amounts in {Currency} and {other.Currency}.");
        }
    }
}
