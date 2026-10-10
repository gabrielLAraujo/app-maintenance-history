using Garage.Domain.Common;

namespace Garage.Application.Abstractions.Ports;

public interface IUnitOfWork
{
    /// <summary>
    /// Despacha os eventos de domínio dos agregados rastreados (até 5 rodadas), grava tudo num único SaveChanges
    /// e então roda as ações de AfterCommit. Conflito de concorrência vira <c>concurrency.conflict</c>.
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken ct);

    /// <summary>Roda as ações de OnRollback e limpa o ChangeTracker.</summary>
    Task DiscardAsync(CancellationToken ct);

    /// <summary>
    /// Ação executada depois do commit (recebe <see cref="CancellationToken.None"/>); falhas só geram log de warning.
    /// </summary>
    void AfterCommit(Func<CancellationToken, Task> action);

    /// <summary>
    /// Ação executada quando o trabalho é descartado (falha do handler, dos eventos ou do SaveChanges);
    /// falhas só geram log de warning.
    /// </summary>
    void OnRollback(Func<CancellationToken, Task> action);
}
