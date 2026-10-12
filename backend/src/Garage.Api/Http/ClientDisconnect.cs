namespace Garage.Api.Http;

/// <summary>Requisição abandonada pelo cliente (aba fechada, navegação cancelada): não é falha do servidor.</summary>
internal static class ClientDisconnect
{
    /// <summary>
    /// Mesmo critério do <c>ExceptionHandlerMiddleware</c> do ASP.NET Core: cancelamento ou E/S interrompida com o
    /// <see cref="HttpContext.RequestAborted"/> já disparado. Um cancelamento com o cliente ainda esperando (timeout interno)
    /// continua sendo erro.
    /// </summary>
    public static bool Caused(HttpContext context, Exception? exception) =>
        exception is OperationCanceledException or IOException && context.RequestAborted.IsCancellationRequested;
}
