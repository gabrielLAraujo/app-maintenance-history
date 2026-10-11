using Garage.Application.Abstractions.Ports;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

internal sealed class VehicleRepository(GarageDbContext db) : IVehicleRepository
{
    public Task<Vehicle?> GetAsync(Guid id, CancellationToken ct) =>
        db.Set<Vehicle>().FirstOrDefaultAsync(v => v.Id == id, ct);

    public void Add(Vehicle v) => db.Set<Vehicle>().Add(v);

    public void Remove(Vehicle v) => db.Set<Vehicle>().Remove(v);

    public void SetExpectedVersion(Vehicle v, uint rowVersion) =>
        db.Entry(v).Property<uint>("RowVersion").OriginalValue = rowVersion;
}
