using Garage.Application.Abstractions.Ports;
using Garage.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

internal sealed class ReminderRepository(GarageDbContext db) : IReminderRepository
{
    public Task<Reminder?> GetAsync(Guid vehicleId, Guid reminderId, CancellationToken ct) =>
        db.Set<Reminder>().FirstOrDefaultAsync(r => r.VehicleId == vehicleId && r.Id == reminderId, ct);

    public async Task<IReadOnlyList<Reminder>> GetManyAsync(
        Guid vehicleId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Set<Reminder>().Where(r => r.VehicleId == vehicleId && ids.Contains(r.Id)).ToListAsync(ct);

    public void Add(Reminder r) => db.Set<Reminder>().Add(r);

    public void Remove(Reminder r) => db.Set<Reminder>().Remove(r);

    public void SetExpectedVersion(Reminder r, uint rowVersion) =>
        db.Entry(r).Property<uint>("RowVersion").OriginalValue = rowVersion;
}
