using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Sharing;
using Garage.Domain.Vehicles;

namespace Garage.Application.Sharing.RevokeShareLink;

internal sealed class RevokeShareLinkHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IShareLinkRepository links,
    IAppClock clock) : ICommandHandler<RevokeShareLinkCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RevokeShareLinkCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var link = await links.GetAsync(command.VehicleId, command.LinkId, ct);
        if (link is null)
            return ShareLinkErrors.NotFound;

        // S2: irreversível; revogar de novo mantém o primeiro instante.
        link.Revoke(clock.Now);
        return Unit.Value;
    }
}
