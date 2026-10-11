using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Reminders;
using Garage.Domain.Vehicles;

namespace Garage.Application.Reminders.UpdateReminder;

internal sealed class UpdateReminderHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IReminderRepository reminders,
    IAppClock clock) : ICommandHandler<UpdateReminderCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateReminderCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        var reminder = await reminders.GetAsync(command.VehicleId, command.ReminderId, ct);
        if (reminder is null)
            return ReminderErrors.NotFound;

        reminders.SetExpectedVersion(reminder, command.RowVersion);

        var input = command.Reminder;
        var baseOdometer = input.ToBaseOdometer();
        var updated = reminder.Update(input.CleanTitle(), input.ToInterval(), input.LastDoneDate, baseOdometer, clock.Today);
        if (updated.IsFailure)
            return updated.Error!;

        // M6: a km da última vez é uma leitura do hodômetro (V1: só vale se for maior que a atual).
        if (baseOdometer is { } km)
            vehicle.RegisterOdometer(km, clock.Now);

        return Unit.Value;
    }
}
