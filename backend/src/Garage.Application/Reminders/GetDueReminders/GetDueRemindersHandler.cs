using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Reminders;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Reminders.GetDueReminders;

internal sealed class GetDueRemindersHandler(
    ICurrentUser currentUser,
    IReadDbContext db,
    IAppClock clock) : IQueryHandler<GetDueRemindersQuery, IReadOnlyList<DueReminderDto>>
{
    public async Task<Result<IReadOnlyList<DueReminderDto>>> Handle(GetDueRemindersQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        // Todos os lembretes dos veículos do usuário: o status não é coluna, então "próximo" e "vencido" só podem ser
        // separados em memória (ReminderSchedule). O volume é de dezenas de linhas por usuário.
        var rows = await (
                from r in db.Query<Reminder>()
                join v in db.Query<Vehicle>() on r.VehicleId equals v.Id
                where v.OwnerId == userId
                select new
                {
                    VehicleId = v.Id,
                    v.Nickname,
                    v.Make,
                    v.Model,
                    ModelYear = v.Year.Model,
                    v.CurrentOdometer,
                    v.OdometerUpdatedAt,
                    ReminderId = r.Id,
                    r.Title,
                    r.IntervalKm,
                    r.IntervalMonths,
                    r.LastDoneDate,
                    r.LastDoneOdometer
                })
            .ToListAsync(ct);

        var today = clock.Today;
        IReadOnlyList<DueReminderDto> due = rows
            .Select(row => new DueReminderDto(
                row.VehicleId,
                // Mesma regra de Vehicle.DisplayName: o apelido, ou "marca modelo ano".
                row.Nickname ?? $"{row.Make} {row.Model} {row.ModelYear}",
                row.ReminderId,
                row.Title,
                ReminderSchedule.Evaluate(
                        row.IntervalKm,
                        row.IntervalMonths,
                        row.LastDoneDate,
                        row.LastDoneOdometer?.Km,
                        row.CurrentOdometer.Km,
                        today)
                    .ToDto(row.OdometerUpdatedAt)))
            .Where(item => item.Status.State != ReminderState.Ok)
            // Vencidos primeiro (o enum vai do melhor ao pior); depois o que acaba antes em dias, e em km. Quem não
            // tem o eixo vai para o fim do critério. O id desempata para a ordem ser estável.
            .OrderByDescending(item => item.Status.State)
            .ThenBy(item => item.Status.DaysRemaining ?? int.MaxValue)
            .ThenBy(item => item.Status.KmRemaining ?? int.MaxValue)
            .ThenBy(item => item.ReminderId)
            .ToList();
        return Result<IReadOnlyList<DueReminderDto>>.Success(due);
    }
}
