using Garage.Application.Abstractions.Ports;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Access;

/// <summary>Fase 1: só o dono lê e edita o veículo. Quem não tem acesso é indistinguível de um veículo inexistente.</summary>
internal sealed class VehicleAccess(IReadDbContext db) : IVehicleAccess
{
    public Task<bool> CanReadAsync(Guid vehicleId, Guid userId, CancellationToken ct) => IsOwnerAsync(vehicleId, userId, ct);

    public Task<bool> CanEditAsync(Guid vehicleId, Guid userId, CancellationToken ct) => IsOwnerAsync(vehicleId, userId, ct);

    private Task<bool> IsOwnerAsync(Guid vehicleId, Guid userId, CancellationToken ct) =>
        db.Query<Vehicle>().AnyAsync(v => v.Id == vehicleId && v.OwnerId == userId, ct);
}
