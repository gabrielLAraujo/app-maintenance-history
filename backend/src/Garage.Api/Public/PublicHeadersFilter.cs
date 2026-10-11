namespace Garage.Api.Public;

/// <summary>
/// Cabeçalhos das respostas públicas (spec 10.3): a visão de um link não deve ser indexada por buscadores nem vazar a URL
/// (com o token) no <c>Referer</c>. São definidos antes do endpoint rodar, então valem também nas respostas de erro (404).
/// </summary>
internal sealed class PublicHeadersFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var headers = context.HttpContext.Response.Headers;
        headers["X-Robots-Tag"] = "noindex";
        headers["Referrer-Policy"] = "no-referrer";

        return next(context);
    }
}
