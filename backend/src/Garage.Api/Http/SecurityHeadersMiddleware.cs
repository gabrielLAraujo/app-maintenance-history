namespace Garage.Api.Http;

/// <summary>
/// Cabeçalhos de segurança de toda resposta, inclusive as de erro: <c>X-Content-Type-Options: nosniff</c>,
/// <c>X-Frame-Options: DENY</c> (nenhuma tela do app é feita para ficar num iframe), <c>Referrer-Policy: no-referrer</c>
/// (a URL da página pública leva o token do link) e, por HTTPS e fora de Development, HSTS.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    private const string Hsts = "max-age=31536000";

    // Em Development (https://localhost do launch profile), um HSTS guardado pelo navegador valeria para todo localhost.
    private readonly bool _hstsEnabled = !environment.IsDevelopment();

    public Task InvokeAsync(HttpContext context)
    {
        // OnStarting: os cabeçalhos valem também para respostas reescritas pelo handler de exceções, que limpa os headers.
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";

            // Atrás do Traefik, IsHttps vem do X-Forwarded-Proto (UseForwardedHeaders roda antes).
            if (_hstsEnabled && context.Request.IsHttps)
                headers["Strict-Transport-Security"] = Hsts;

            return Task.CompletedTask;
        });

        return next(context);
    }
}
