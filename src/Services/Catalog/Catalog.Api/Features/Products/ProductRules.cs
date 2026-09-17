using Catalog.Api.Domain;
using FluentValidation;

namespace Catalog.Api.Features.Products;

/// <summary>Field rules shared by the create and update slices, so both enforce the same limits as the database.</summary>
internal static class ProductRules
{
    public const int MinVolumeMl = 100;
    public const int MaxVolumeMl = 5000;
    public const int MaxPackSize = 48;

    public static IRuleBuilderOptions<T, string> ValidSku<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MaximumLength(Product.SkuMaxLength)
            .Matches("^[A-Za-z0-9]+(-[A-Za-z0-9]+)*$")
            .WithMessage("SKU must contain letters, digits and single hyphens (e.g. GOLDEN-LAGER-350).");

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Product.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ValidStyle<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .IsEnumName(typeof(BeerStyle), caseSensitive: false)
            .WithMessage($"Style must be one of: {string.Join(", ", Enum.GetNames<BeerStyle>())}.");

    public static IRuleBuilderOptions<T, int> ValidVolumeMl<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(MinVolumeMl, MaxVolumeMl);

    public static IRuleBuilderOptions<T, int> ValidPackSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, MaxPackSize);

    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilder<T, decimal> rule) =>
        rule
            .GreaterThan(0)
            .PrecisionScale(Product.PricePrecision, Product.PriceScale, ignoreTrailingZeros: true);
}
