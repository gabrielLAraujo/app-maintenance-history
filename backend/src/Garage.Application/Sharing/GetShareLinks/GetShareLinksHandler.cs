using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Sharing;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Sharing.GetShareLinks;

internal sealed class GetShareLinksHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db,
    IAppClock clock) : IQueryHandler<GetShareLinksQuery, IReadOnlyList<ShareLinkDto>>
{
    public async Task<Result<IReadOnlyList<ShareLinkDto>>> Handle(GetShareLinksQuery query, CancellationToken ct)
    {
        if (!await access.CanReadAsync(query.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        // Os revogados e expirados continuam na lista (inativos), para o dono saber o que já compartilhou.
        var links = await db.Query<ShareLink>()
            .Where(l => l.VehicleId == query.VehicleId)
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .ToListAsync(ct);

        var now = clock.Now;
        IReadOnlyList<ShareLinkDto> dtos = links.Select(link => link.ToDto(now)).ToList();
        return Result<IReadOnlyList<ShareLinkDto>>.Success(dtos);
    }
}
