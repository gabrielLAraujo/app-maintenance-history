using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Reminders;
using Garage.Domain.Vehicles;

namespace Garage.Application.Reminders.CreateReminder;

internal sealed class CreateReminderHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IVehicleRepository vehicles,
    IReminderRepository reminders,
    IAppClock clock) : ICommandHandler<CreateReminderCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateReminderCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await vehicles.GetAsync(command.VehicleId, ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        var input = command.Reminder;
        var baseOdometer = input.ToBaseOdometer();
        var created = Reminder.Create(
            vehicle.Id, input.CleanTitle(), input.ToInterval(), input.LastDoneDate, baseOdometer, clock.Today);
        if (created.IsFailure)
            return created.Error!;

        // M6: a km da última vez é uma leitura do hodômetro (V1: só vale se for maior que a atual).
        if (baseOdometer is { } km)
            vehicle.RegisterOdometer(km, clock.Now);

        reminders.Add(created.Value);
        return created.Value.Id;
    }
}
