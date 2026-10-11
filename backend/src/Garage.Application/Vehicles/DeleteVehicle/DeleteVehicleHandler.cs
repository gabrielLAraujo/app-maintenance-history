using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles.DeleteVehicle;

internal sealed class DeleteVehicleHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IAttachmentRepository attachments,
    IFileStorage storage,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteVehicleCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteVehicleCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        // Registros, peças, lembretes e anexos saem em cascata; os arquivos, só depois do commit (spec 5.5). As chaves são
        // lidas antes, enquanto as linhas existem. Uma ação por arquivo: a falha de um não impede apagar os outros.
        foreach (var key in await attachments.GetStorageKeysByVehicleAsync(vehicle.Id, ct))
            unitOfWork.AfterCommit(afterCt => storage.DeleteAsync(key, afterCt));

        vehicles.Remove(vehicle);
        return Unit.Value;
    }
}
