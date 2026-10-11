using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Attachments.GetAttachmentContent;

public sealed record GetAttachmentContentQuery(Guid VehicleId, Guid AttachmentId) : IQuery<FileContent>;
