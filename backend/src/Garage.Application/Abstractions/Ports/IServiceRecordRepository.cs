using Garage.Domain.Records;

namespace Garage.Application.Abstractions.Ports;

/// <summary>
/// Repositório do agregado <see cref="ServiceRecord"/> para os comandos (as consultas usam <see cref="IReadDbContext"/>).
/// Os registros são sempre buscados pelo veículo <b>e</b> pelo id; não verificam acesso: use <see cref="IVehicleAccess"/> antes.
/// </summary>
public interface IServiceRecordRepository
{
    /// <summary>Carrega o registro do veículo, com as peças, para alteração (rastreado); <c>null</c> se não existir nele.</summary>
    Task<ServiceRecord?> GetAsync(Guid vehicleId, Guid recordId, CancellationToken ct);

    /// <summary>
    /// Limites de km de R3 para um registro em <paramref name="date"/>: a maior km entre os registros com data estritamente
    /// anterior e a menor entre os com data estritamente posterior. Ignora registros sem km e o <paramref name="excludeRecordId"/>.
    /// </summary>
    Task<OdometerBounds> GetOdometerBoundsAsync(Guid vehicleId, DateOnly date, Guid? excludeRecordId, CancellationToken ct);

    /// <summary>A maior km entre os registros do veículo; <c>null</c> se nenhum tiver km.</summary>
    Task<int?> GetMaxOdometerAsync(Guid vehicleId, CancellationToken ct);

    void Add(ServiceRecord r);

    void Remove(ServiceRecord r);

    /// <summary>
    /// Informa a versão (<c>RowVersion</c>) que o cliente leu: o próximo <c>SaveChanges</c> só altera o registro se o
    /// banco ainda estiver nela; senão o <see cref="IUnitOfWork"/> devolve <c>concurrency.conflict</c>. Vale também para
    /// uma alteração só das peças.
    /// </summary>
    void SetExpectedVersion(ServiceRecord r, uint rowVersion);
}
