using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;

namespace Garage.Application.Records.UpdateServiceRecord;

internal sealed class UpdateServiceRecordHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IServiceRecordRepository records,
    IAppClock clock) : ICommandHandler<UpdateServiceRecordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateServiceRecordCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var record = await records.GetAsync(command.VehicleId, command.RecordId, ct);
        if (record is null)
            return ServiceRecordErrors.NotFound;

        records.SetExpectedVersion(record, command.RowVersion);

        // R3 na edição: o próprio registro sai do cálculo dos limites. R6: lembretes não são tocados.
        var details = command.Record.ToDetails();
        var bounds = await records.GetOdometerBoundsAsync(record.VehicleId, details.Date, record.Id, ct);
        var updated = record.Update(details, bounds, clock.Today);
        if (updated.IsFailure)
            return updated.Error!;

        // R4: a km do registro é uma leitura do hodômetro (V1: só vale se for maior que a atual).
        if (details.Odometer is { } km)
        {
            var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
            if (vehicle is null)
                return VehicleErrors.NotFound;

            vehicle.RegisterOdometer(km, clock.Now);
        }

        return Unit.Value;
    }
}
