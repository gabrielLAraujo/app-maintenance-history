using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Sharing.RevokeShareLink;

public sealed record RevokeShareLinkCommand(Guid VehicleId, Guid LinkId) : ICommand<Unit>;
