using System.Collections.Concurrent;
using System.Reflection;
using Garage.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Application.Abstractions.Events;

internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    private const string HandleMethodName = "Handle";

    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Handle)> Handlers = new();

    public async Task<Result> Dispatch(IReadOnlyList<IDomainEvent> events, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var e in events)
        {
            var (handlerType, handle) = Handlers.GetOrAdd(e.GetType(), static eventType =>
            {
                var type = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
                return (type, type.GetMethod(HandleMethodName)!);
            });

            foreach (var handler in services.GetServices(handlerType))
            {
                var result = await (Task<Result>)handle.Invoke(
                    handler, BindingFlags.DoNotWrapExceptions, binder: null, [e, ct], culture: null)!;

                if (result.IsFailure)
                    return result;
            }
        }

        return Result.Success();
    }
}
