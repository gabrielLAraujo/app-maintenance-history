using Garage.Domain.Common;

namespace Garage.Domain.Attachments;

public static class AttachmentErrors
{
    public static readonly Error NotFound = new(
        "attachment.not_found",
        "Anexo não encontrado.",
        ErrorType.NotFound);

    public static readonly Error TooLarge = new(
        "attachment.too_large",
        "Arquivo maior que 10 MB.",
        ErrorType.PayloadTooLarge);

    public static readonly Error UnsupportedType = new(
        "attachment.unsupported_type",
        "Formato não suportado. Envie JPG, PNG, WebP ou PDF.",
        ErrorType.UnsupportedMedia);

    public static readonly Error LimitReached = new(
        "attachment.limit_reached",
        "Limite de 20 anexos por registro atingido.",
        ErrorType.DomainRule);

    public static readonly Error DocumentCannotBePublic = new(
        "attachment.document_cannot_be_public",
        "Documentos não podem ficar visíveis no link público.",
        ErrorType.DomainRule);
}
