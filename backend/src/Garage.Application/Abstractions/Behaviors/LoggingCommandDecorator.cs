using System.Diagnostics;
using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Garage.Application.Abstractions.Behaviors;

internal sealed class LoggingCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<LoggingCommandDecorator<TCommand, TResult>> logger) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> Handle(TCommand command, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await inner.Handle(command, ct);
        UseCaseLog.Completed(logger, typeof(TCommand).Name, result, Stopwatch.GetElapsedTime(started));
        return result;
    }
}
