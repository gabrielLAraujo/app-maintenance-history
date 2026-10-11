using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Reminders.CreateReminder;

public sealed record CreateReminderCommand(Guid VehicleId, ReminderInput Reminder) : ICommand<Guid>;
