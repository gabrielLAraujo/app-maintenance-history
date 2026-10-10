using System.Diagnostics;
using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Garage.Application.Abstractions.Behaviors;

internal sealed class LoggingQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<LoggingQueryDecorator<TQuery, TResult>> logger) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<Result<TResult>> Handle(TQuery query, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await inner.Handle(query, ct);
        UseCaseLog.Completed(logger, typeof(TQuery).Name, result, Stopwatch.GetElapsedTime(started));
        return result;
    }
}
