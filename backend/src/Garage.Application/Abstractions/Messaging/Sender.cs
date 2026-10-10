using System.Collections.Concurrent;
using System.Reflection;
using Garage.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Application.Abstractions.Messaging;

internal sealed class Sender(IServiceProvider services) : ISender
{
    private const string HandleMethodName = "Handle";

    private static readonly ConcurrentDictionary<Type, HandlerDescriptor> CommandHandlers = new();
    private static readonly ConcurrentDictionary<Type, HandlerDescriptor> QueryHandlers = new();

    public Task<Result<TResult>> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Dispatch<TResult>(CommandHandlers, typeof(ICommandHandler<,>), command, ct);
    }

    public Task<Result<TResult>> Send<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Dispatch<TResult>(QueryHandlers, typeof(IQueryHandler<,>), query, ct);
    }

    private Task<Result<TResult>> Dispatch<TResult>(
        ConcurrentDictionary<Type, HandlerDescriptor> cache, Type openHandlerType, object request, CancellationToken ct)
    {
        var descriptor = cache.GetOrAdd(
            request.GetType(),
            static (requestType, state) => HandlerDescriptor.For(state.OpenHandlerType, requestType, state.ResultType),
            (OpenHandlerType: openHandlerType, ResultType: typeof(TResult)));

        var handler = services.GetRequiredService(descriptor.HandlerType);
        return (Task<Result<TResult>>)descriptor.Handle.Invoke(
            handler, BindingFlags.DoNotWrapExceptions, binder: null, [request, ct], culture: null)!;
    }

    private sealed record HandlerDescriptor(Type HandlerType, MethodInfo Handle)
    {
        public static HandlerDescriptor For(Type openHandlerType, Type requestType, Type resultType)
        {
            var handlerType = openHandlerType.MakeGenericType(requestType, resultType);
            return new HandlerDescriptor(handlerType, handlerType.GetMethod(HandleMethodName)!);
        }
    }
}
