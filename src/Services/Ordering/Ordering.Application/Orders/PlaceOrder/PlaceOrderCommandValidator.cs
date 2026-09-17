using FluentValidation;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.Orders.PlaceOrder;

/// <summary>
/// Input validation (shape of the request). Field rules reuse the value object factories, so the limits are
/// defined once, in the domain; the aggregate still enforces the same rules as its last line of defense.
/// </summary>
internal sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.CustomerEmail).NotEmpty().EmailAddress();

        RuleFor(command => command.Items)
            .NotEmpty()
            .WithMessage("An order must contain at least one item.")
            .Must(items => items is null || items.Count <= Order.MaxItems)
            .WithMessage($"An order cannot contain more than {Order.MaxItems} different products.")
            .Must(HaveUniqueSkus)
            .WithMessage("Each SKU can appear only once; combine repeated SKUs into a single item.");

        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Sku).Custom((sku, context) =>
            {
                var result = Sku.Create(sku);
                if (result.IsFailure)
                {
                    context.AddFailure(result.Error.Description);
                }
            });

            item.RuleFor(i => i.Quantity).InclusiveBetween(1, Quantity.Max);
        });
    }

    private static bool HaveUniqueSkus(IReadOnlyList<PlaceOrderItem>? items) =>
        items is null ||
        items.Where(item => item?.Sku is not null)
            .Select(item => item.Sku.Trim().ToUpperInvariant())
            .GroupBy(sku => sku, StringComparer.Ordinal)
            .All(group => group.Count() == 1);
}
