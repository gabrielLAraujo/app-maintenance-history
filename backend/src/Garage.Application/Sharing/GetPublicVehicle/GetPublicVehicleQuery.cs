using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Sharing.GetPublicVehicle;

/// <summary>Visão pública do veículo de um link. <see cref="Cursor"/> é o <c>nextCursor</c> da página anterior.</summary>
public sealed record GetPublicVehicleQuery(string Token, string? Cursor) : IQuery<PublicVehicleDto>
{
    public const int PageSize = 30;
}
