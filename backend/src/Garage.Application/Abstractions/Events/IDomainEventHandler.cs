using Garage.Domain.Common;

namespace Garage.Application.Abstractions.Events;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task<Result> Handle(TEvent e, CancellationToken ct);
}
