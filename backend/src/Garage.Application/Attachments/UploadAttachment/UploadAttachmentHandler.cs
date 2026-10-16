using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Demo;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Attachments.UploadAttachment;

internal sealed class UploadAttachmentHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db,
    IAttachmentRepository attachments,
    IImageProcessor images,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IAppClock clock) : ICommandHandler<UploadAttachmentCommand, AttachmentDto>
{
    private const string PdfContentType = "application/pdf";
    private const int CopyBufferSize = 81_920;

    public async Task<Result<AttachmentDto>> Handle(UploadAttachmentCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        // Spec 3.2: a demo não recebe arquivos (não vira hospedagem).
        if (currentUser.IsDemo)
            return DemoErrors.UploadsDisabled;

        var recordExists = await db.Query<ServiceRecord>()
            .AnyAsync(r => r.VehicleId == command.VehicleId && r.Id == command.RecordId, ct);
        if (!recordExists)
            return ServiceRecordErrors.NotFound;

        // A1 e A3 antes de ler o arquivo.
        if (command.Length > Attachment.MaxSizeBytes)
            return AttachmentErrors.TooLarge;
        if (await attachments.CountByRecordAsync(command.RecordId, ct) >= Attachment.MaxPerRecord)
            return AttachmentErrors.LimitReached;

        // O tamanho informado não basta: a cópia também para no limite.
        await using var buffer = new MemoryStream((int)Math.Clamp(command.Length, 0, Attachment.MaxSizeBytes));
        if (!await CopyWithinLimitAsync(command.Content, buffer, ct))
            return AttachmentErrors.TooLarge;

        // A2: o formato vem dos primeiros bytes, nunca do nome nem do Content-Type enviados.
        if (FileSignature.Detect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)) is not { } type)
            return AttachmentErrors.UnsupportedType;

        buffer.Position = 0;
        var processed = await ProcessAsync(buffer, type, ct);
        if (processed is null)
            return AttachmentErrors.UnsupportedType;

        await using var content = processed.Content;
        var attachment = Attachment.Create(
            command.VehicleId,
            command.RecordId,
            command.Kind,
            type,
            AttachmentMapping.CleanFileName(command.FileName),
            processed.ContentType,
            processed.Length,
            clock.Now);

        // Registrado antes de gravar: se a gravação ou o commit falharem (ou a requisição for cancelada no meio), o
        // arquivo não fica órfão no disco. Apagar uma chave que não chegou a ser gravada não faz nada.
        unitOfWork.OnRollback(rollbackCt => storage.DeleteAsync(attachment.StorageKey, rollbackCt));
        await storage.SaveAsync(attachment.StorageKey, content, ct);

        attachments.Add(attachment);
        return attachment.ToDto();
    }

    /// <summary>A5: imagens são reprocessadas; PDFs seguem como vieram. <c>null</c> se a imagem não decodificar.</summary>
    private async Task<ProcessedFile?> ProcessAsync(MemoryStream buffer, DetectedFileType type, CancellationToken ct)
    {
        if (type == DetectedFileType.Pdf)
            return new ProcessedFile(buffer, PdfContentType, buffer.Length);

        try
        {
            return await images.NormalizeAsync(buffer, type, ct);
        }
        catch (InvalidDataException)
        {
            // Assinatura certa, conteúdo inválido (ou imagem grande demais para decodificar): o formato não é aceito.
            return null;
        }
    }

    private static async Task<bool> CopyWithinLimitAsync(Stream source, MemoryStream target, CancellationToken ct)
    {
        var chunk = new byte[CopyBufferSize];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (target.Length + read > Attachment.MaxSizeBytes)
                return false;

            target.Write(chunk, 0, read);
        }

        return true;
    }
}
