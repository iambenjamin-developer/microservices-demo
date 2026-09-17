using FluentValidation;

namespace Inventory.Api.Features.Stock.GetStock;

internal sealed class GetStockValidator : AbstractValidator<GetStockRequest>
{
    public const int MaxSkus = 100;

    public GetStockValidator() =>
        RuleFor(request => request.Skus)
            .Must(skus => skus is null || skus.Length <= MaxSkus)
            .WithMessage($"At most {MaxSkus} SKUs can be requested at once.");
}
