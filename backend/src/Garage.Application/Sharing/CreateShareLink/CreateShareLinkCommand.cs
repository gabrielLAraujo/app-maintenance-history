using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Sharing.CreateShareLink;

public sealed record CreateShareLinkCommand(
    Guid VehicleId,
    string? Label,
    bool IncludeCosts,
    bool IncludePlate,
    DateTimeOffset? ExpiresAt) : ICommand<ShareLinkDto>;
