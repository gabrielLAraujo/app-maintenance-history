using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles.CorrectOdometer;

internal sealed class CorrectOdometerHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IServiceRecordRepository records,
    IAppClock clock) : ICommandHandler<CorrectOdometerCommand, Unit>
{
    public async Task<Result<Unit>> Handle(CorrectOdometerCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        // V3: pode diminuir (correção de digitação), mas não abaixo da maior km do histórico.
        var maxRecordKm = await records.GetMaxOdometerAsync(vehicle.Id, ct);
        var corrected = vehicle.CorrectOdometer(
            new Odometer(command.Km), maxRecordKm is { } max ? new Odometer(max) : null, clock.Now);
        if (corrected.IsFailure)
            return corrected.Error!;

        return Unit.Value;
    }
}
