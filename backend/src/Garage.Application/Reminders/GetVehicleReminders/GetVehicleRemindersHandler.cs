using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Reminders;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Reminders.GetVehicleReminders;

internal sealed class GetVehicleRemindersHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db,
    IAppClock clock) : IQueryHandler<GetVehicleRemindersQuery, IReadOnlyList<ReminderDto>>
{
    public async Task<Result<IReadOnlyList<ReminderDto>>> Handle(GetVehicleRemindersQuery query, CancellationToken ct)
    {
        if (!await access.CanReadAsync(query.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await db.Query<Vehicle>()
            .Where(v => v.Id == query.VehicleId)
            .Select(v => new { v.CurrentOdometer, v.OdometerUpdatedAt })
            .FirstOrDefaultAsync(ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        // O status não é coluna: é calculado em memória (ReminderSchedule) contra a km atual do veículo e a data de hoje.
        var rows = await db.Query<Reminder>()
            .Where(r => r.VehicleId == query.VehicleId)
            .OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.IntervalKm,
                r.IntervalMonths,
                r.LastDoneDate,
                r.LastDoneOdometer,
                RowVersion = EF.Property<uint>(r, "RowVersion")
            })
            .ToListAsync(ct);

        var today = clock.Today;
        IReadOnlyList<ReminderDto> reminders = rows
            .Select(row => new ReminderDto(
                row.Id,
                row.Title,
                row.IntervalKm,
                row.IntervalMonths,
                row.LastDoneDate,
                row.LastDoneOdometer?.Km,
                ReminderSchedule.Evaluate(
                        row.IntervalKm,
                        row.IntervalMonths,
                        row.LastDoneDate,
                        row.LastDoneOdometer?.Km,
                        vehicle.CurrentOdometer.Km,
                        today)
                    .ToDto(vehicle.OdometerUpdatedAt),
                row.RowVersion))
            .ToList();
        return Result<IReadOnlyList<ReminderDto>>.Success(reminders);
    }
}
