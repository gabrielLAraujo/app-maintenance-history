using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles.DeleteVehicle;

internal sealed class DeleteVehicleHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles) : ICommandHandler<DeleteVehicleCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteVehicleCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        // A remoção dos arquivos em disco (spec 5.5) entra junto com os anexos.
        vehicles.Remove(vehicle);
        return Unit.Value;
    }
}
