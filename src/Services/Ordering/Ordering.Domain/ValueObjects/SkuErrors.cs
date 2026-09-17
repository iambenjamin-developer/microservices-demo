using BuildingBlocks.Common.Results;

namespace Ordering.Domain.ValueObjects;

public static class SkuErrors
{
    public static readonly Error Empty =
        Error.Validation("Sku.Empty", "The SKU is required.");

    public static readonly Error TooLong =
        Error.Validation("Sku.TooLong", $"The SKU cannot be longer than {Sku.MaxLength} characters.");

    public static Error InvalidFormat(string sku) =>
        Error.Validation("Sku.InvalidFormat", $"'{sku}' must contain letters, digits and single hyphens (e.g. GOLDEN-LAGER-350).");
}
