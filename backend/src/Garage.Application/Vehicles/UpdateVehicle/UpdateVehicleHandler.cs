using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles.UpdateVehicle;

internal sealed class UpdateVehicleHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IAppClock clock) : ICommandHandler<UpdateVehicleCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateVehicleCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        vehicles.SetExpectedVersion(vehicle, command.RowVersion);
        vehicle.Update(command.Vehicle.ToData(clock.CurrentYear));
        return Unit.Value;
    }
}
