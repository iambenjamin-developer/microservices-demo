using FluentValidation;

namespace Inventory.Api.Features.Stock.UpdateStock;

internal sealed class UpdateStockValidator : AbstractValidator<UpdateStockRequest>
{
    public const int MaxQuantityAvailable = 1_000_000;

    public UpdateStockValidator() =>
        RuleFor(request => request.QuantityAvailable).InclusiveBetween(0, MaxQuantityAvailable);
}
