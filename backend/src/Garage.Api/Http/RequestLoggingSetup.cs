using Serilog;
using Serilog.Events;

namespace Garage.Api.Http;

/// <summary>
/// Uma linha de log por requisição (Serilog). O caminho sai mascarado pelo <see cref="PublicTokenMasking"/>, registrado no
/// logger do host.
/// </summary>
public static class RequestLoggingSetup
{
    public static IApplicationBuilder UseRequestLogging(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseSerilogRequestLogging(options =>
        {
            // O logger deste host, não o Log.Logger estático: com mais de um host no processo (testes), cada um usa o seu.
            options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
            options.GetLevel = GetLevel;
        });
    }

    /// <summary>
    /// O padrão do Serilog (exceção ou 5xx é Error, o resto Information), menos a requisição que o cliente abandonou: o
    /// cancelamento propaga uma exceção, mas não é falha do servidor.
    /// </summary>
    internal static LogEventLevel GetLevel(HttpContext context, double elapsedMs, Exception? exception)
    {
        if (ClientDisconnect.Caused(context, exception))
            return LogEventLevel.Information;

        return exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    }
}
