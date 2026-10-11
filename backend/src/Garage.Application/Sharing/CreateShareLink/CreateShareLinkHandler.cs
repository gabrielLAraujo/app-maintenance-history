using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Sharing;
using Garage.Domain.Vehicles;

namespace Garage.Application.Sharing.CreateShareLink;

internal sealed class CreateShareLinkHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IShareLinkRepository links,
    IAppClock clock) : ICommandHandler<CreateShareLinkCommand, ShareLinkDto>
{
    public async Task<Result<ShareLinkDto>> Handle(CreateShareLinkCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var now = clock.Now;
        var link = ShareLink.Create(
            command.VehicleId,
            ShareTokens.New(),
            string.IsNullOrWhiteSpace(command.Label) ? null : command.Label.Trim(),
            command.IncludeCosts,
            command.IncludePlate,
            // O Npgsql só grava timestamptz com offset zero; o instante é o mesmo.
            command.ExpiresAt?.ToUniversalTime(),
            now);

        links.Add(link);
        return link.ToDto(now);
    }
}
