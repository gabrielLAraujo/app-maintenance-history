using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Reminders;
using Garage.Domain.Vehicles;

namespace Garage.Application.Reminders.DeleteReminder;

internal sealed class DeleteReminderHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReminderRepository reminders) : ICommandHandler<DeleteReminderCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteReminderCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var reminder = await reminders.GetAsync(command.VehicleId, command.ReminderId, ct);
        if (reminder is null)
            return ReminderErrors.NotFound;

        reminders.Remove(reminder);
        return Unit.Value;
    }
}
