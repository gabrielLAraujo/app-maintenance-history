using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Reminders.GetAllReminders;

public sealed record GetAllRemindersQuery : IQuery<IReadOnlyList<DueReminderDto>>;
