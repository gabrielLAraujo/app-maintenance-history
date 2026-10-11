using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Reminders.GetVehicleReminders;

public sealed record GetVehicleRemindersQuery(Guid VehicleId) : IQuery<IReadOnlyList<ReminderDto>>;
