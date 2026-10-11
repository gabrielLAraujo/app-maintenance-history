using Garage.Application.Abstractions.Ports;
using Garage.Domain.Sharing;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence.Repositories;

internal sealed class ShareLinkRepository(GarageDbContext db) : IShareLinkRepository
{
    public Task<ShareLink?> GetAsync(Guid vehicleId, Guid linkId, CancellationToken ct) =>
        db.Set<ShareLink>().FirstOrDefaultAsync(l => l.VehicleId == vehicleId && l.Id == linkId, ct);

    public void Add(ShareLink l) => db.Set<ShareLink>().Add(l);
}
