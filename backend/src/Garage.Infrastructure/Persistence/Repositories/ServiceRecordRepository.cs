using Garage.Application.Abstractions.Ports;
using Garage.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

internal sealed class ServiceRecordRepository(GarageDbContext db) : IServiceRecordRepository
{
    // As peças são owned: vêm junto com o registro.
    public Task<ServiceRecord?> GetAsync(Guid vehicleId, Guid recordId, CancellationToken ct) =>
        db.Set<ServiceRecord>().FirstOrDefaultAsync(r => r.VehicleId == vehicleId && r.Id == recordId, ct);

    public async Task<OdometerBounds> GetOdometerBoundsAsync(
        Guid vehicleId, DateOnly date, Guid? excludeRecordId, CancellationToken ct)
    {
        // MAX/MIN ignoram os registros sem km; datas iguais à do registro não restringem em nenhum dos lados.
        var others = db.Set<ServiceRecord>().AsNoTracking().Where(r => r.VehicleId == vehicleId);
        if (excludeRecordId is { } excluded)
            others = others.Where(r => r.Id != excluded);

        var maxKmBefore = await others.Where(r => r.Date < date).MaxAsync(r => r.Odometer, ct);
        var minKmAfter = await others.Where(r => r.Date > date).MinAsync(r => r.Odometer, ct);
        return new OdometerBounds(maxKmBefore?.Km, minKmAfter?.Km);
    }

    public async Task<int?> GetMaxOdometerAsync(Guid vehicleId, CancellationToken ct) =>
        (await db.Set<ServiceRecord>().Where(r => r.VehicleId == vehicleId).MaxAsync(r => r.Odometer, ct))?.Km;

    public void Add(ServiceRecord r) => db.Set<ServiceRecord>().Add(r);

    public void Remove(ServiceRecord r) => db.Set<ServiceRecord>().Remove(r);

    public void SetExpectedVersion(ServiceRecord r, uint rowVersion)
    {
        var entry = db.Entry(r);
        entry.Property<uint>("RowVersion").OriginalValue = rowVersion;

        // As peças ficam em outra tabela: uma edição só delas não tocaria a linha do registro, e a versão não seria
        // conferida nem avançaria. Marcar o registro como alterado garante o UPDATE com "WHERE xmin = versão lida".
        entry.State = EntityState.Modified;
    }
}
