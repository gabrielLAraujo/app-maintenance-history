using Garage.Application.Abstractions.Ports;
using Garage.Domain.Reminders;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Reminders;

/// <summary>
/// Todos os lembretes dos veículos do usuário, com o status calculado e o pior estado primeiro. Base de
/// <c>GET /reminders</c> (todos) e de <c>GET /reminders/due</c> (só vencidos e próximos).
/// </summary>
internal sealed class UserReminderStatuses(IReadDbContext db, IAppClock clock)
{
    public async Task<IReadOnlyList<DueReminderDto>> LoadAsync(Guid userId, CancellationToken ct)
    {
        // O status não é coluna, então "em dia", "próximo" e "vencido" só podem ser separados em memória
        // (ReminderSchedule). O volume é de dezenas de linhas por usuário.
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
        return rows
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
            // Vencidos primeiro (o enum vai do melhor ao pior); depois o que acaba antes em dias, e em km. Quem não
            // tem o eixo vai para o fim do critério. O id desempata para a ordem ser estável.
            .OrderByDescending(item => item.Status.State)
            .ThenBy(item => item.Status.DaysRemaining ?? int.MaxValue)
            .ThenBy(item => item.Status.KmRemaining ?? int.MaxValue)
            .ThenBy(item => item.ReminderId)
            .ToList();
    }
}
