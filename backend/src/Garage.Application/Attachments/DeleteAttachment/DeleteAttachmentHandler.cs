using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Attachments.DeleteAttachment;

internal sealed class DeleteAttachmentHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IAttachmentRepository attachments,
    IFileStorage storage,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteAttachmentCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteAttachmentCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var attachment = await attachments.GetAsync(command.VehicleId, command.AttachmentId, ct);
        if (attachment is null)
            return AttachmentErrors.NotFound;

        attachments.Remove(attachment);
        // O arquivo só sai do disco depois do commit (spec 5.5); se apagar falhar, fica só o log.
        var key = attachment.StorageKey;
        unitOfWork.AfterCommit(afterCt => storage.DeleteAsync(key, afterCt));
        return Unit.Value;
    }
}
