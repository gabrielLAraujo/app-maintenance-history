using Garage.Domain.Common;

namespace Garage.Application.Abstractions.Events;

public interface IDomainEventDispatcher
{
    /// <summary>Despacha os eventos em ordem para todos os handlers; para no primeiro resultado de falha.</summary>
    Task<Result> Dispatch(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}
