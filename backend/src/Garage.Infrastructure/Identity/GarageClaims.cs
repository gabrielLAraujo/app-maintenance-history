namespace Garage.Infrastructure.Identity;

/// <summary>Claims próprias da Garagem, emitidas por <see cref="AppUserClaimsPrincipalFactory"/>.</summary>
public static class GarageClaims
{
    /// <summary>Presente, com o valor <c>"true"</c>, só na sessão de uma conta demo.</summary>
    public const string Demo = "garage:demo";
}
