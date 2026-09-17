using FluentValidation;

namespace Catalog.Api.Features.Products.GetProducts;

internal sealed class GetProductsValidator : AbstractValidator<GetProductsRequest>
{
    public const int MaxSkus = 100;

    public GetProductsValidator() =>
        RuleFor(request => request.Skus)
            .Must(skus => skus is null || skus.Length <= MaxSkus)
            .WithMessage($"At most {MaxSkus} SKUs can be requested at once.");
}
