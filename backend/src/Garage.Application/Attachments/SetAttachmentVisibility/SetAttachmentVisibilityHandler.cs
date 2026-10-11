using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Attachments.SetAttachmentVisibility;

internal sealed class SetAttachmentVisibilityHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IAttachmentRepository attachments) : ICommandHandler<SetAttachmentVisibilityCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SetAttachmentVisibilityCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var attachment = await attachments.GetAsync(command.VehicleId, command.AttachmentId, ct);
        if (attachment is null)
            return AttachmentErrors.NotFound;

        // A4: só foto pode ficar pública.
        var changed = attachment.SetVisibility(command.IsPublic);
        return changed.IsSuccess ? Unit.Value : changed.Error!;
    }
}
