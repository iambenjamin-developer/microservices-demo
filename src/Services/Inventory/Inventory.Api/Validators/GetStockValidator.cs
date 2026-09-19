using FluentValidation;
using Inventory.Api.Contracts;

namespace Inventory.Api.Validators;

internal sealed class GetStockValidator : AbstractValidator<GetStockRequest>
{
    public const int MaxSkus = 100;

    public GetStockValidator() =>
        RuleFor(request => request.Skus)
            .Must(skus => skus is null || skus.Length <= MaxSkus)
            .WithMessage($"At most {MaxSkus} SKUs can be requested at once.");
}
