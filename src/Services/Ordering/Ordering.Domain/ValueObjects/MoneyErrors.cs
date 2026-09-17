using BuildingBlocks.Common.Results;

namespace Ordering.Domain.ValueObjects;

public static class MoneyErrors
{
    public static readonly Error NegativeAmount =
        Error.Validation("Money.NegativeAmount", "An amount cannot be negative.");

    public static readonly Error InvalidScale =
        Error.Validation("Money.InvalidScale", $"An amount cannot have more than {Money.Scale} decimal places.");

    public static Error InvalidCurrency(string? currency) =>
        Error.Validation("Money.InvalidCurrency", $"'{currency}' is not a valid ISO 4217 currency code.");
}
