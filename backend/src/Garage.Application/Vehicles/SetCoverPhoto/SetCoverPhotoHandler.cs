using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles.SetCoverPhoto;

internal sealed class SetCoverPhotoHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IAttachmentRepository attachments) : ICommandHandler<SetCoverPhotoCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SetCoverPhotoCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        if (command.AttachmentId is not { } attachmentId)
        {
            vehicle.ClearCoverPhoto();
            return Unit.Value;
        }

        // Busca por veículo e id (IDOR): anexo de outro veículo, ou inexistente, é a mesma resposta da regra C1.
        var attachment = await attachments.GetAsync(command.VehicleId, attachmentId, ct);
        if (attachment is null)
            return VehicleErrors.CoverMustBeVehiclePhoto;

        var set = vehicle.SetCoverPhoto(attachment);
        return set.IsSuccess ? Unit.Value : set.Error!;
    }
}
