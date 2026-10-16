using Garage.Application.Abstractions.Ports;
using Garage.Application.Attachments;
using Garage.Application.Sharing;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Reminders;
using Garage.Domain.Sharing;
using Garage.Domain.Vehicles;

namespace Garage.Application.Demo;

/// <summary>
/// Os dados de exemplo para um dono: usado pelo botão "Carregar dados de exemplo" (<c>SeedDemoDataCommand</c>, conta real
/// vazia) e pela demo por visitante (<c>SeedVisitorDemoCommand</c>, conta demo recém-criada).
/// </summary>
internal sealed class DemoDataSeeder(
    IVehicleRepository vehicles,
    IServiceRecordRepository records,
    IReminderRepository reminders,
    IShareLinkRepository shareLinks,
    IAttachmentRepository attachments,
    IDemoAssets assets,
    IImageProcessor images,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IAppClock clock)
{
    private const string PdfContentType = "application/pdf";

    /// <summary>
    /// Cria os veículos de exemplo (<see cref="DemoDataCatalog"/>) para <paramref name="ownerId"/>, sem gravar nada: quem chama
    /// é um comando, e o <c>TransactionCommandDecorator</c> faz um único SaveChanges.
    /// </summary>
    public async Task<Result<DemoSeedResult>> SeedAsync(Guid ownerId, CancellationToken ct)
    {
        var today = clock.Today;
        var now = clock.Now;
        var demoVehicles = DemoDataCatalog.Build(today);

        // Uma falha em qualquer item (que o catálogo não deve ter) descarta tudo, sem deixar metade dos dados de exemplo. Os
        // arquivos dos anexos vão para o disco já durante o laço, mas cada um com a limpeza registrada antes (OnRollback),
        // como no upload.
        foreach (var demo in demoVehicles)
        {
            var vehicle = Vehicle.Create(ownerId, demo.Data, demo.CurrentOdometer, now);
            vehicles.Add(vehicle);

            foreach (var item in demo.Records)
            {
                var created = ServiceRecord.Create(vehicle.Id, item.Details, BoundsFor(item.Details, demo.Records), [], today, now);
                if (created.IsFailure)
                    return created.Error!;

                records.Add(created.Value);

                foreach (var file in item.Attachments)
                {
                    var attached = await AttachAsync(vehicle.Id, created.Value.Id, file, now, ct);
                    if (attached.IsFailure)
                        return attached.Error!;

                    if (file.IsCover)
                    {
                        var cover = vehicle.SetCoverPhoto(attached.Value);
                        if (cover.IsFailure)
                            return cover.Error!;
                    }
                }
            }

            foreach (var item in demo.Reminders)
            {
                var reminder = Reminder.Create(
                    vehicle.Id, item.Title, item.Interval, item.LastDoneDate, item.LastDoneOdometer, today);
                if (reminder.IsFailure)
                    return reminder.Error!;

                reminders.Add(reminder.Value);
            }

            if (demo.PublicLink is { } link)
            {
                shareLinks.Add(ShareLink.Create(
                    vehicle.Id, ShareTokens.New(), link.Label, link.IncludeCosts, link.IncludePlate, expiresAt: null, now));
            }
        }

        return new DemoSeedResult(
            demoVehicles.Count,
            demoVehicles.Sum(demo => demo.Records.Count),
            demoVehicles.Sum(demo => demo.Reminders.Count));
    }

    /// <summary>
    /// O mesmo caminho do <c>UploadAttachmentHandler</c> (A2 e A5): o tipo vem dos primeiros bytes, as fotos passam pelo
    /// <see cref="IImageProcessor"/> (sem metadados, sRGB, no máximo 2560 px, JPEG q85) e o PDF segue como veio. A
    /// visibilidade vem do catálogo (o domínio recusa documento público, A4).
    /// </summary>
    private async Task<Result<Attachment>> AttachAsync(Guid vehicleId, Guid recordId, DemoAttachment file, DateTimeOffset now, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await using (var asset = assets.Open(file.Asset))
            await asset.CopyToAsync(buffer, ct);

        if (FileSignature.Detect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)) is not { } type)
            return AttachmentErrors.UnsupportedType;

        buffer.Position = 0;
        var processed = await ProcessAsync(buffer, type, ct);
        if (processed is null)
            return AttachmentErrors.UnsupportedType;

        await using var content = processed.Content;
        var attachment = Attachment.Create(
            vehicleId,
            recordId,
            file.Kind,
            type,
            AttachmentMapping.CleanFileName(file.FileName),
            processed.ContentType,
            processed.Length,
            now);

        var visibility = attachment.SetVisibility(file.IsPublic);
        if (visibility.IsFailure)
            return visibility.Error!;

        // Registrado antes de gravar: se a gravação, outro item do seed ou o commit falharem, o arquivo não fica órfão no
        // disco. Apagar uma chave que não chegou a ser gravada não faz nada.
        unitOfWork.OnRollback(rollbackCt => storage.DeleteAsync(attachment.StorageKey, rollbackCt));
        await storage.SaveAsync(attachment.StorageKey, content, ct);

        attachments.Add(attachment);
        return attachment;
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
            return null;
        }
    }

    /// <summary>
    /// R3 sem consultar o banco (nada foi gravado ainda): a maior km entre os registros mais antigos e a menor entre os mais
    /// novos, como <see cref="IServiceRecordRepository.GetOdometerBoundsAsync"/> calcularia.
    /// </summary>
    private static OdometerBounds BoundsFor(RecordDetails details, IReadOnlyList<DemoRecord> all) => new(
        all.Where(other => other.Details.Date < details.Date).Select(other => other.Details.Odometer?.Km).Max(),
        all.Where(other => other.Details.Date > details.Date).Select(other => other.Details.Odometer?.Km).Min());
}
