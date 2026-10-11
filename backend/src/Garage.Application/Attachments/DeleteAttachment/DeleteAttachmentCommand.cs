using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Attachments.DeleteAttachment;

public sealed record DeleteAttachmentCommand(Guid VehicleId, Guid AttachmentId) : ICommand<Unit>;
