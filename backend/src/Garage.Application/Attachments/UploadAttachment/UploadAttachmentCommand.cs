using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Attachments;

namespace Garage.Application.Attachments.UploadAttachment;

/// <summary>
/// Envio de um arquivo para o registro. <see cref="Length"/> é o tamanho recebido; <see cref="FileName"/>, o nome original
/// (só para exibição). O formato é sempre detectado pelos primeiros bytes de <see cref="Content"/>.
/// </summary>
public sealed record UploadAttachmentCommand(
    Guid VehicleId,
    Guid RecordId,
    Stream Content,
    long Length,
    string FileName,
    AttachmentKind Kind) : ICommand<AttachmentDto>;
