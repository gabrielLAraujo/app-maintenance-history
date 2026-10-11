using FluentValidation;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Sharing;

namespace Garage.Application.Sharing.CreateShareLink;

internal sealed class CreateShareLinkValidator : AbstractValidator<CreateShareLinkCommand>
{
    public CreateShareLinkValidator(IAppClock clock)
    {
        RuleFor(x => x.Label)
            .MaximumLength(ShareLink.MaxLabelLength).WithMessage("O rótulo deve ter no máximo {MaxLength} caracteres.");

        // Um link que já nasce expirado seria inútil: a validade, quando informada, precisa ser futura.
        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt > clock.Now).WithMessage("A validade do link deve ser uma data futura.")
            .When(x => x.ExpiresAt is not null);
    }
}
