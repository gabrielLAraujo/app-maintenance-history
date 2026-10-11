using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Attachments;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Garage.Application.Sharing.GetPublicAttachment;

/// <summary>
/// Anônimo (spec 10.4): só a foto pública de um veículo cujo link é válido. Qualquer outro caso — token malformado, link
/// inexistente, revogado ou expirado, anexo de outro veículo, documento ou foto privada — é o mesmo
/// <c>attachment.not_found</c>, sem distinguir o motivo. O token malformado nem chega ao banco.
/// </summary>
internal sealed partial class GetPublicAttachmentHandler(
    IReadDbContext db,
    IFileStorage storage,
    IAppClock clock,
    ILogger<GetPublicAttachmentHandler> logger) : IQueryHandler<GetPublicAttachmentQuery, FileContent>
{
    private const string PngContentType = "image/png";

    public async Task<Result<FileContent>> Handle(GetPublicAttachmentQuery query, CancellationToken ct)
    {
        if (!ShareTokens.LooksValid(query.Token))
            return AttachmentErrors.NotFound;

        var link = await db.Query<ShareLink>().FirstOrDefaultAsync(l => l.Token == query.Token, ct);
        if (link is null || !link.IsValidAt(clock.Now))
            return AttachmentErrors.NotFound;

        var attachment = await db.Query<Attachment>()
            .Where(a => a.VehicleId == link.VehicleId
                        && a.Id == query.AttachmentId
                        && a.Kind == AttachmentKind.Photo
                        && a.IsPublic)
            .Select(a => new { a.StorageKey, a.ContentType })
            .FirstOrDefaultAsync(ct);
        if (attachment is null)
            return AttachmentErrors.NotFound;

        var content = await storage.OpenReadAsync(attachment.StorageKey, ct);
        if (content is null)
        {
            // Linha sem arquivo: perda de dados no armazenamento (ex.: pasta fora de um volume persistente).
            LogFileMissing(logger, query.AttachmentId);
            return AttachmentErrors.NotFound;
        }

        return new FileContent(content, attachment.ContentType, PublicFileName(query.AttachmentId, attachment.ContentType));
    }

    // O nome original é do dono e pode identificar pessoas, lugares ou o carro: o visitante recebe só o id do anexo. As fotos
    // são gravadas como JPEG ou PNG (o processamento converte o resto para JPEG).
    private static string PublicFileName(Guid attachmentId, string contentType) =>
        contentType == PngContentType ? $"{attachmentId}.png" : $"{attachmentId}.jpg";

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Attachment {AttachmentId} has no file in storage")]
    private static partial void LogFileMissing(ILogger logger, Guid attachmentId);
}
