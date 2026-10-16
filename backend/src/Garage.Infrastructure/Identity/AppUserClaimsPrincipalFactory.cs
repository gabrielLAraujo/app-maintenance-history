using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Identity;

/// <summary>
/// Acrescenta <see cref="GarageClaims.Demo"/> à sessão das contas demo. Passa também pela renovação do cookie
/// (SecurityStampValidator), então a claim não se perde ao recarregar a sessão.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory(UserManager<AppUser> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.IsDemo)
            identity.AddClaim(new Claim(GarageClaims.Demo, "true"));

        return identity;
    }
}
