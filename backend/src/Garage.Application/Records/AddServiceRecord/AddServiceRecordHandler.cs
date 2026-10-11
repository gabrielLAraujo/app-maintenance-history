using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;

namespace Garage.Application.Records.AddServiceRecord;

internal sealed class AddServiceRecordHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IServiceRecordRepository records,
    IAppClock clock) : ICommandHandler<AddServiceRecordCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddServiceRecordCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        var details = command.Record.ToDetails();
        var bounds = await records.GetOdometerBoundsAsync(vehicle.Id, details.Date, excludeRecordId: null, ct);

        // R5: o ServiceRecordAdded leva os lembretes cumpridos; FulfillRemindersOnRecordAdded os atualiza no SaveChanges.
        var created = ServiceRecord.Create(vehicle.Id, details, bounds, command.FulfilledReminderIds, clock.Today, clock.Now);
        if (created.IsFailure)
            return created.Error!;

        // R4: a km do registro é uma leitura do hodômetro (V1: só vale se for maior que a atual).
        if (details.Odometer is { } km)
            vehicle.RegisterOdometer(km, clock.Now);

        records.Add(created.Value);
        return created.Value.Id;
    }
}
