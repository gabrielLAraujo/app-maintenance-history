using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Reminders.UpdateReminder;

public sealed record UpdateReminderCommand(Guid VehicleId, Guid ReminderId, ReminderInput Reminder, uint RowVersion)
    : ICommand<Unit>;
