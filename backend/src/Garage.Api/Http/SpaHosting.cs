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

    // Rotas do front: qualquer caminho sem extensão de arquivo que não seja da API nem da documentação. O fallback nunca
    // pode engolir /api (inclusive health e rotas inexistentes, que devem seguir dando 404 da API), /openapi e /scalar;
    // o regex é desde o início do caminho e vale para o prefixo inteiro ("api" ou "api/...", mas não "apiary").
    private const string FallbackPattern = "{*path:nonfile:regex(^(?!(api|openapi|scalar)(/|$)).*$)}";

    /// <summary>Arquivos estáticos do <c>wwwroot</c>, antes da autenticação: não passam por login, antiforgery nem rate limit.</summary>
    public static IApplicationBuilder UseSpaStaticFiles(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseStaticFiles(CreateStaticFileOptions());
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

        // A raiz vai à parte: o regex de FallbackPattern só aceita caminhos com valor, e em "/" o catch-all não tem nenhum.
        app.MapFallbackToFile("/", IndexFile, CreateStaticFileOptions()).ExcludeFromDescription();
        app.MapFallbackToFile(FallbackPattern, IndexFile, CreateStaticFileOptions()).ExcludeFromDescription();
    }

    private static StaticFileOptions CreateStaticFileOptions() => new()
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl =
            context.Context.Request.Path.StartsWithSegments("/assets") ? ImmutableCache : RevalidateCache
    };
}
