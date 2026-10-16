using Garage.Application.Abstractions.Ports;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Garage.Api.Auth;

/// <summary>
/// Sessão de conta demo só vale enquanto a conta vale (spec 3.2): vencida, encerrada pelo logout ou já apagada pela
/// limpeza, o cookie é recusado em toda a API, não só no <c>/auth/me</c>. Roda depois da checagem do security stamp do
/// Identity e só consulta o banco para quem tem a claim <see cref="GarageClaims.Demo"/>.
/// </summary>
internal static class DemoSessionValidator
{
    public static Func<CookieValidatePrincipalContext, Task> After(Func<CookieValidatePrincipalContext, Task> inner) =>
        async context =>
        {
            await inner(context);
            if (context.Principal?.HasClaim(GarageClaims.Demo, "true") != true)
                return;

            var services = context.HttpContext.RequestServices;
            var user = await services.GetRequiredService<UserManager<AppUser>>().GetUserAsync(context.Principal);
            if (user is not null && user.IsDemo && user.DemoExpiresAt > services.GetRequiredService<IAppClock>().Now)
                return;

            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        };
}
