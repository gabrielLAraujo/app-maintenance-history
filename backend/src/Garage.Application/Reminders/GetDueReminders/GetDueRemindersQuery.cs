using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Reminders.GetDueReminders;

public sealed record GetDueRemindersQuery : IQuery<IReadOnlyList<DueReminderDto>>;
