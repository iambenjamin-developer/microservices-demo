using FluentValidation;

namespace Gateway.Authentication;

internal sealed class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(request => request.Username).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(200);
    }
}
