namespace Garage.Application.Abstractions.Ports;

public interface IVehicleAccess
{
    Task<bool> CanReadAsync(Guid vehicleId, Guid userId, CancellationToken ct);

    Task<bool> CanEditAsync(Guid vehicleId, Guid userId, CancellationToken ct);
}
