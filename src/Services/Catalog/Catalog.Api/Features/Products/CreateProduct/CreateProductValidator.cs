using FluentValidation;

namespace Catalog.Api.Features.Products.CreateProduct;

internal sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(request => request.Sku).ValidSku();
        RuleFor(request => request.Name).ValidName();
        RuleFor(request => request.Style).ValidStyle();
        RuleFor(request => request.VolumeMl).ValidVolumeMl();
        RuleFor(request => request.PackSize).ValidPackSize();
        RuleFor(request => request.Price).ValidPrice();
    }
}
