using Microsoft.AspNetCore.StaticFiles;

namespace Garage.Api.Http;

/// <summary>
/// Serve o front (build do Vite copiado para <c>wwwroot</c>) pela própria API, no mesmo domínio: cookie e antiforgery
/// sem CORS. Sem <c>wwwroot/index.html</c> (testes, desenvolvimento com o servidor do Vite) o fallback nem é mapeado.
/// </summary>
public static class SpaHosting
{
    private const string IndexFile = "index.html";

    // Os arquivos de /assets têm hash no nome (o Vite troca o nome a cada mudança), então nunca mudam de conteúdo.
    private const string ImmutableCache = "public,max-age=31536000,immutable";

    // O resto (index.html, ícones) é revalidado a cada visita: o deploy novo aparece na hora, e o ETag evita o download.
    private const string RevalidateCache = "no-cache";

    // O caminho pedido fica neste valor de rota: o MapFallbackToFile troca Request.Path por /index.html antes de servir.
    private const string PathParameter = "path";

    // Rotas do front: qualquer caminho sem extensão de arquivo que não seja da API nem da documentação. O fallback nunca
    // pode engolir /api (inclusive health e rotas inexistentes, que devem seguir dando 404 da API), /openapi e /scalar;
    // o regex é desde o início do caminho e vale para o prefixo inteiro ("api" ou "api/...", mas não "apiary").
    private const string FallbackPattern = "{*" + PathParameter + ":nonfile:regex(^(?!(api|openapi|scalar)(/|$)).*$)}";

    // A página pública (/p/{token}) é a visão de um link: não deve ser indexada por buscadores (spec 10.3).
    private const string PublicPagePrefix = "p/";

    /// <summary>Arquivos estáticos do <c>wwwroot</c>, antes da autenticação: não passam por login, antiforgery nem rate limit.</summary>
    public static IApplicationBuilder UseSpaStaticFiles(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = PrepareFileResponse });
    }

    /// <summary>
    /// Qualquer rota do front que o navegador peça direto (<c>/vehicles/abc</c> com F5, <c>/p/token</c>) devolve o
    /// <c>index.html</c>, e o React Router resolve. Chamar depois de mapear os endpoints.
    /// </summary>
    public static void MapSpaFallback(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.WebRootFileProvider.GetFileInfo(IndexFile).Exists)
            return;

        var options = new StaticFileOptions { OnPrepareResponse = PrepareIndexResponse };

        // A raiz vai à parte: o regex de FallbackPattern só aceita caminhos com valor, e em "/" o catch-all não tem nenhum.
        app.MapFallbackToFile("/", IndexFile, options).ExcludeFromDescription();
        app.MapFallbackToFile(FallbackPattern, IndexFile, options).ExcludeFromDescription();
    }

    // Só arquivos que existem no wwwroot chegam aqui; em /assets, os do build, com hash no nome.
    private static void PrepareFileResponse(StaticFileResponseContext context) =>
        context.Context.Response.Headers.CacheControl =
            context.Context.Request.Path.StartsWithSegments("/assets") ? ImmutableCache : RevalidateCache;

    // O index.html no lugar de uma rota do front é sempre revalidado, qualquer que seja o caminho pedido (inclusive
    // /assets/<sem extensão>): o cache imutável fica só para os arquivos de verdade.
    private static void PrepareIndexResponse(StaticFileResponseContext context)
    {
        var http = context.Context;
        http.Response.Headers.CacheControl = RevalidateCache;

        if (http.Request.RouteValues[PathParameter] is string path
            && path.StartsWith(PublicPagePrefix, StringComparison.OrdinalIgnoreCase))
            http.Response.Headers["X-Robots-Tag"] = "noindex";
    }
}
