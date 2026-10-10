using Garage.Domain.Common;

namespace Garage.Domain.Attachments;

public sealed class Attachment : AggregateRoot
{
    /// <summary>A1: tamanho máximo de um arquivo (10 MB).</summary>
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    /// <summary>A2: quantidade máxima de anexos por registro.</summary>
    public const int MaxPerRecord = 20;

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

    /// <summary>Chave do arquivo no armazenamento: <c>{VehicleId}/{Id}</c> (calculada, não persistida).</summary>
    public string StorageKey => $"{VehicleId}/{Id}";

    /// <summary>
    /// A3: PDF é sempre <see cref="AttachmentKind.Document"/>; nos demais formatos vale o tipo pedido.
    /// Só foto nasce pública. A1 (tamanho) e A2 (limite por registro) são checadas pelo handler antes de criar.
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
        var kind = type == DetectedFileType.Pdf ? AttachmentKind.Document : requestedKind;

        return new Attachment
        {
            Id = Guid.CreateVersion7(),
            VehicleId = vehicleId,
            RecordId = recordId,
            Kind = kind,
            IsPublic = kind == AttachmentKind.Photo,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedAt = now
        };
    }

    /// <summary>A4: documento nunca fica visível no link público; em caso de falha, o estado não muda.</summary>
    public Result SetVisibility(bool isPublic)
    {
        if (isPublic && Kind == AttachmentKind.Document)
            return AttachmentErrors.DocumentCannotBePublic;

        IsPublic = isPublic;
        return Result.Success();
    }
}
