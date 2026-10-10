using Garage.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Garage.Application.Abstractions.Behaviors;

internal static partial class UseCaseLog
{
    public static void Completed(ILogger logger, string useCase, Result result, TimeSpan elapsed)
    {
        if (result.IsSuccess)
            Succeeded(logger, useCase, elapsed.TotalMilliseconds);
        else
            Failed(logger, useCase, result.Error!.Code, elapsed.TotalMilliseconds);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "{UseCase} succeeded in {ElapsedMs:0.0} ms")]
    private static partial void Succeeded(ILogger logger, string useCase, double elapsedMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "{UseCase} failed with {ErrorCode} in {ElapsedMs:0.0} ms")]
    private static partial void Failed(ILogger logger, string useCase, string errorCode, double elapsedMs);
}
