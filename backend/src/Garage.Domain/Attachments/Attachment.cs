using Garage.Domain.Common;

namespace Garage.Domain.Attachments;

public sealed class Attachment : AggregateRoot
{
    /// <summary>A1: tamanho máximo de um arquivo (10 MB).</summary>
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    /// <summary>A3: quantidade máxima de anexos por registro.</summary>
    public const int MaxPerRecord = 20;

    /// <summary>Tamanho máximo do nome original guardado para exibição (spec 4.6).</summary>
    public const int MaxFileNameLength = 200;

    // Construtor sem parâmetros apenas para a materialização do EF Core; os inicializadores
    // `null!` satisfazem a análise de nulos e são sempre sobrescritos na materialização.
    private Attachment()
    {
    }

    public Guid VehicleId { get; private set; }

    public Guid RecordId { get; private set; }

    public AttachmentKind Kind { get; private set; }

    public bool IsPublic { get; private set; }

    public string FileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>
    /// Chave do arquivo no armazenamento: <c>{VehicleId}/{Id}</c>, gerada pelo sistema na criação (nunca vem do nome
    /// enviado). É persistida para que a limpeza dos arquivos de um registro ou veículo a leia direto do banco.
    /// </summary>
    public string StorageKey { get; private set; } = null!;

    /// <summary>
    /// PDF é sempre <see cref="AttachmentKind.Document"/>; nos demais formatos vale o tipo pedido. Só foto nasce pública
    /// (A4). O handler checa antes de criar: A1 (tamanho), A2 (tipo pelos primeiros bytes, via <see cref="FileSignature"/>)
    /// e A3 (limite por registro).
    /// </summary>
    public static Attachment Create(
        Guid vehicleId,
        Guid recordId,
        AttachmentKind requestedKind,
        DetectedFileType type,
        string fileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset now)
    {
        var id = Guid.CreateVersion7();
        var kind = type == DetectedFileType.Pdf ? AttachmentKind.Document : requestedKind;

        return new Attachment
        {
            Id = id,
            VehicleId = vehicleId,
            RecordId = recordId,
            Kind = kind,
            IsPublic = kind == AttachmentKind.Photo,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedAt = now,
            StorageKey = $"{vehicleId}/{id}"
        };
    }

    /// <summary>
    /// A4: só foto pode ficar visível no link público (documento — ou qualquer outro tipo — nunca); em caso de falha,
    /// o estado não muda.
    /// </summary>
    public Result SetVisibility(bool isPublic)
    {
        if (isPublic && Kind != AttachmentKind.Photo)
            return AttachmentErrors.DocumentCannotBePublic;

        IsPublic = isPublic;
        return Result.Success();
    }
}
