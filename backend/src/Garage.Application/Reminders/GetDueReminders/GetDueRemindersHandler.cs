using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Reminders;

namespace Garage.Application.Reminders.GetDueReminders;

internal sealed class GetDueRemindersHandler(
    ICurrentUser currentUser,
    UserReminderStatuses statuses) : IQueryHandler<GetDueRemindersQuery, IReadOnlyList<DueReminderDto>>
{
    public async Task<Result<IReadOnlyList<DueReminderDto>>> Handle(GetDueRemindersQuery query, CancellationToken ct)
    {
        var all = await statuses.LoadAsync(currentUser.UserId, ct);

        IReadOnlyList<DueReminderDto> due = all.Where(item => item.Status.State != ReminderState.Ok).ToList();
        return Result<IReadOnlyList<DueReminderDto>>.Success(due);
    }
}
