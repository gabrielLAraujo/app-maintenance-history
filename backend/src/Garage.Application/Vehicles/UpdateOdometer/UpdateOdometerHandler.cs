using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles.UpdateOdometer;

internal sealed class UpdateOdometerHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IAppClock clock) : ICommandHandler<UpdateOdometerCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateOdometerCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        var updated = vehicle.UpdateOdometer(new Odometer(command.Km), clock.Now);
        if (updated.IsFailure)
            return updated.Error!;

        return Unit.Value;
    }
}
