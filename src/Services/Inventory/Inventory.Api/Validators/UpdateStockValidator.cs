using FluentValidation;
using Inventory.Api.Contracts;

namespace Inventory.Api.Validators;

internal sealed class UpdateStockValidator : AbstractValidator<UpdateStockRequest>
{
    public const int MaxQuantityAvailable = 1_000_000;

    public UpdateStockValidator() =>
        RuleFor(request => request.QuantityAvailable).InclusiveBetween(0, MaxQuantityAvailable);
}
