using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Reminders.DeleteReminder;

public sealed record DeleteReminderCommand(Guid VehicleId, Guid ReminderId) : ICommand<Unit>;
