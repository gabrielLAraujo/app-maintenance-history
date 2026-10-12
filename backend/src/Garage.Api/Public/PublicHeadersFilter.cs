namespace Garage.Api.Public;

/// <summary>
/// Cabeçalho das respostas públicas (spec 10.3): a visão de um link não deve ser indexada por buscadores. É definido antes
/// do endpoint rodar, então vale também nas respostas de erro (404). O <c>Referrer-Policy: no-referrer</c>, que impede a URL
/// (com o token) de vazar no <c>Referer</c>, vem do <c>SecurityHeadersMiddleware</c>, em toda resposta.
/// </summary>
internal sealed class PublicHeadersFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex";

        return next(context);
    }
}
