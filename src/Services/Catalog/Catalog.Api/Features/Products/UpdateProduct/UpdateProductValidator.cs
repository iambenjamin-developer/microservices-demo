using FluentValidation;

namespace Catalog.Api.Features.Products.UpdateProduct;

internal sealed class UpdateProductValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductValidator()
    {
        RuleFor(request => request.Name).ValidName();
        RuleFor(request => request.Style).ValidStyle();
        RuleFor(request => request.VolumeMl).ValidVolumeMl();
        RuleFor(request => request.PackSize).ValidPackSize();
        RuleFor(request => request.Price).ValidPrice();
    }
}
