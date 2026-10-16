using System.Security.Claims;
using Garage.Api.Endpoints;
using Garage.Application.Abstractions.Ports;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Garage.Api.Auth;

/// <summary>A sessão atual. <see cref="DemoExpiresAt"/> só nas contas demo.</summary>
public sealed record MeResponse(Guid Id, string Email, bool IsDemo, DateTimeOffset? DemoExpiresAt);

internal sealed class MeEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/auth/me", HandleAsync)
            .WithName("GetCurrentUser")
            .WithTags("Auth")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, IAppClock clock)
    {
        // Confere no banco: um cookie ainda válido de um usuário removido não é uma sessão; nem o de uma demo vencida
        // (inclusive a que saiu pelo logout e espera a limpeza).
        var user = await userManager.GetUserAsync(principal);
        if (user is null || (user.IsDemo && user.DemoExpiresAt <= clock.Now))
            return TypedResults.Unauthorized();

        return TypedResults.Ok(new MeResponse(user.Id, user.Email!, user.IsDemo, user.DemoExpiresAt));
    }
}
