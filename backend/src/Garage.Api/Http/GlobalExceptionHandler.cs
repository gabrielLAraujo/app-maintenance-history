using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Api.Http;

/// <summary>Exceção não tratada → 500 ProblemDetails com <c>traceId</c>, sem mensagem nem stack trace.</summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // O cliente desistiu da requisição: não é erro do servidor e não há quem leia um corpo. O ExceptionHandlerMiddleware
        // do .NET 10 já separa esse caso antes de chamar os handlers; a checagem aqui não depende disso.
        if (ClientDisconnect.Caused(httpContext, exception))
        {
            LogClientDisconnected(logger, httpContext.Request.Method, httpContext.Request.Path.Value);
            if (!httpContext.Response.HasStarted)
                httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;

            return true;
        }

        LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Ocorreu um erro inesperado. Tente novamente em instantes."
            }
        });
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, string? path);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Request {Method} {Path} aborted by the client")]
    private static partial void LogClientDisconnected(ILogger logger, string method, string? path);
}
