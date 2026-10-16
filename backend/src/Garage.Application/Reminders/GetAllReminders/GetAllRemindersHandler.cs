using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;

namespace Garage.Application.Reminders.GetAllReminders;

/// <summary>Todos os lembretes do usuário, inclusive os em dia, com o pior estado primeiro.</summary>
internal sealed class GetAllRemindersHandler(
    ICurrentUser currentUser,
    UserReminderStatuses statuses) : IQueryHandler<GetAllRemindersQuery, IReadOnlyList<DueReminderDto>>
{
    public async Task<Result<IReadOnlyList<DueReminderDto>>> Handle(GetAllRemindersQuery query, CancellationToken ct)
    {
        var all = await statuses.LoadAsync(currentUser.UserId, ct);
        return Result<IReadOnlyList<DueReminderDto>>.Success(all);
    }
}
