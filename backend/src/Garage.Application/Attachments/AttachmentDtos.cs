using Garage.Domain.Attachments;

namespace Garage.Application.Attachments;

public sealed record AttachmentDto(
    Guid Id,
    AttachmentKind Kind,
    bool IsPublic,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);

/// <summary>Arquivo de um anexo para download. Quem recebe descarta o stream.</summary>
public sealed record FileContent(Stream Content, string ContentType, string FileName);
