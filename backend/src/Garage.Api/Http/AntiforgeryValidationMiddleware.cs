using Microsoft.AspNetCore.Antiforgery;

namespace Garage.Api.Http;

/// <summary>
/// Exige o token antiforgery (cabeçalho <c>X-XSRF-TOKEN</c>) em todo POST, PUT, PATCH e DELETE sob <c>/api</c>.
/// O token é vinculado ao usuário: depois de login ou registro o cliente busca <c>/api/auth/antiforgery</c> de novo.
/// </summary>
internal sealed class AntiforgeryValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsMutatingApiRequest(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await ApiErrors.AntiforgeryInvalid.ToProblem().ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }

    private static bool IsMutatingApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api")
        && (HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method)
            || HttpMethods.IsPatch(request.Method) || HttpMethods.IsDelete(request.Method));
}
