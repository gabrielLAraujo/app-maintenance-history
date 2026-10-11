using Garage.Domain.Vehicles;

namespace Garage.Application.Abstractions.Ports;

/// <summary>Repositório do agregado <see cref="Vehicle"/> para os comandos (as consultas usam <see cref="IReadDbContext"/>).</summary>
public interface IVehicleRepository
{
    /// <summary>Carrega o veículo para alteração (rastreado); <c>null</c> se não existir. Não verifica acesso: use <see cref="IVehicleAccess"/> antes.</summary>
    Task<Vehicle?> GetAsync(Guid id, CancellationToken ct);

    void Add(Vehicle v);

    void Remove(Vehicle v);

    /// <summary>
    /// Informa a versão (<c>RowVersion</c>) que o cliente leu: o próximo <c>SaveChanges</c> só altera o veículo se o
    /// banco ainda estiver nela; senão o <see cref="IUnitOfWork"/> devolve <c>concurrency.conflict</c>.
    /// </summary>
    void SetExpectedVersion(Vehicle v, uint rowVersion);
}
