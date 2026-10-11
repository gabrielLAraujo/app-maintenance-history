using Garage.Domain.Sharing;

namespace Garage.Application.Abstractions.Ports;

/// <summary>
/// Repositório do agregado <see cref="ShareLink"/> para os comandos (as consultas usam <see cref="IReadDbContext"/>).
/// Os links são sempre buscados pelo veículo <b>e</b> pelo id; não verificam acesso: use <see cref="IVehicleAccess"/> antes.
/// </summary>
public interface IShareLinkRepository
{
    /// <summary>Carrega o link do veículo para alteração (rastreado); <c>null</c> se não existir nele.</summary>
    Task<ShareLink?> GetAsync(Guid vehicleId, Guid linkId, CancellationToken ct);

    void Add(ShareLink l);
}
