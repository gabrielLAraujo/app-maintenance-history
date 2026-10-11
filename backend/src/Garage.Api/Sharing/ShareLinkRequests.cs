using Garage.Application.Sharing.CreateShareLink;

namespace Garage.Api.Sharing;

/// <summary>Corpo de <c>POST /vehicles/{vehicleId}/share-links</c>; sem os campos, o link nasce sem custos, sem placa e sem validade.</summary>
public sealed record CreateShareLinkRequest(
    string? Label,
    bool IncludeCosts,
    bool IncludePlate,
    DateTimeOffset? ExpiresAt)
{
    internal CreateShareLinkCommand ToCommand(Guid vehicleId) =>
        new(vehicleId, Label, IncludeCosts, IncludePlate, ExpiresAt);
}
