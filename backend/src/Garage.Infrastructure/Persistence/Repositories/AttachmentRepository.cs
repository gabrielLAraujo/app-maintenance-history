using Garage.Application.Abstractions.Ports;
using Garage.Domain.Attachments;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

internal sealed class AttachmentRepository(GarageDbContext db) : IAttachmentRepository
{
    public Task<Attachment?> GetAsync(Guid vehicleId, Guid attachmentId, CancellationToken ct) =>
        db.Set<Attachment>().FirstOrDefaultAsync(a => a.VehicleId == vehicleId && a.Id == attachmentId, ct);

    public Task<int> CountByRecordAsync(Guid recordId, CancellationToken ct) =>
        db.Set<Attachment>().CountAsync(a => a.RecordId == recordId, ct);

    public async Task<IReadOnlyList<string>> GetStorageKeysByVehicleAsync(Guid vehicleId, CancellationToken ct) =>
        await db.Set<Attachment>().Where(a => a.VehicleId == vehicleId).Select(a => a.StorageKey).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetStorageKeysByRecordAsync(Guid recordId, CancellationToken ct) =>
        await db.Set<Attachment>().Where(a => a.RecordId == recordId).Select(a => a.StorageKey).ToListAsync(ct);

    public void Add(Attachment a) => db.Set<Attachment>().Add(a);

    public void Remove(Attachment a) => db.Set<Attachment>().Remove(a);
}
