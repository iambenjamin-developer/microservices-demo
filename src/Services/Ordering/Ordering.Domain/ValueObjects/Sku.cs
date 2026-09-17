using System.Text.RegularExpressions;
using BuildingBlocks.Common.Results;

namespace Ordering.Domain.ValueObjects;

/// <summary>
/// Stock keeping unit: the product identity shared by Catalog, Ordering and Inventory.
/// Always stored in upper case so comparisons across services are consistent.
/// </summary>
public sealed partial record Sku
{
    public const int MaxLength = 50;

    private Sku(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Sku> Create(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalized.Length == 0)
        {
            return SkuErrors.Empty;
        }

        if (normalized.Length > MaxLength)
        {
            return SkuErrors.TooLong;
        }

        return SkuFormat().IsMatch(normalized)
            ? new Sku(normalized)
            : SkuErrors.InvalidFormat(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9]+(-[A-Z0-9]+)*$")]
    private static partial Regex SkuFormat();
}
