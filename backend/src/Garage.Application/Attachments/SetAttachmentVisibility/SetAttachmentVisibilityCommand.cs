using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Attachments.SetAttachmentVisibility;

public sealed record SetAttachmentVisibilityCommand(Guid VehicleId, Guid AttachmentId, bool IsPublic) : ICommand<Unit>;
