using FluentValidation;

namespace Garage.Application.Attachments.UploadAttachment;

internal sealed class UploadAttachmentValidator : AbstractValidator<UploadAttachmentCommand>
{
    public UploadAttachmentValidator() =>
        // Sem padrão: um tipo fora do enum não pode virar foto (pública) nem documento por engano.
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Tipo de anexo inválido.");
}
