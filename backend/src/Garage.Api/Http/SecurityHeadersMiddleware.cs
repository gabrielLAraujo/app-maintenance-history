namespace Garage.Api.Http;

/// <summary>Cabeçalhos de segurança de toda resposta (<c>X-Content-Type-Options: nosniff</c>).</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        // OnStarting: o cabeçalho vale também para respostas reescritas pelo handler de exceções, que limpa os headers.
        context.Response.OnStarting(static state =>
        {
            ((HttpContext)state).Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}
