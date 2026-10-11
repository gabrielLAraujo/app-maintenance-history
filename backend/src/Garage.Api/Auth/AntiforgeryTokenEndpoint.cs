using Garage.Api.Endpoints;
using Microsoft.AspNetCore.Antiforgery;

namespace Garage.Api.Auth;

internal sealed class AntiforgeryTokenEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/auth/antiforgery", Handle)
            .AllowAnonymous()
            .WithName("GetAntiforgeryToken")
            .WithTags("Auth")
            .Produces(StatusCodes.Status204NoContent);

    // O cookie XSRF-TOKEN precisa ser legível pelo front (não HttpOnly), que devolve o valor em X-XSRF-TOKEN.
    private static IResult Handle(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(AuthSetup.AntiforgeryCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        return TypedResults.NoContent();
    }
}
