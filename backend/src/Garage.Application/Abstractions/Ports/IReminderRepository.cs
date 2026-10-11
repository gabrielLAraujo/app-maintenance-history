using Garage.Domain.Reminders;

namespace Garage.Application.Abstractions.Ports;

/// <summary>
/// Repositório do agregado <see cref="Reminder"/> para os comandos (as consultas usam <see cref="IReadDbContext"/>).
/// Os lembretes são sempre buscados pelo veículo <b>e</b> pelo id; não verificam acesso: use <see cref="IVehicleAccess"/> antes.
/// </summary>
public interface IReminderRepository
{
    /// <summary>Carrega o lembrete do veículo para alteração (rastreado); <c>null</c> se não existir nele.</summary>
    Task<Reminder?> GetAsync(Guid vehicleId, Guid reminderId, CancellationToken ct);

    /// <summary>Carrega, para alteração (rastreados), os lembretes de <paramref name="ids"/> que pertencem ao veículo; os demais ids são ignorados.</summary>
    Task<IReadOnlyList<Reminder>> GetManyAsync(Guid vehicleId, IReadOnlyCollection<Guid> ids, CancellationToken ct);

    void Add(Reminder r);

    void Remove(Reminder r);

    /// <summary>
    /// Informa a versão (<c>RowVersion</c>) que o cliente leu: o próximo <c>SaveChanges</c> só altera o lembrete se o
    /// banco ainda estiver nela; senão o <see cref="IUnitOfWork"/> devolve <c>concurrency.conflict</c>.
    /// </summary>
    void SetExpectedVersion(Reminder r, uint rowVersion);
}
