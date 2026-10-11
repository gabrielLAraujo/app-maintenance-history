using System.Security.Claims;
using Garage.Api.Endpoints;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Garage.Api.Auth;

public sealed record MeResponse(Guid Id, string Email);

internal sealed class MeEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/auth/me", HandleAsync)
            .WithName("GetCurrentUser")
            .WithTags("Auth")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(ClaimsPrincipal principal, UserManager<AppUser> userManager)
    {
        // Confere no banco: um cookie ainda válido de um usuário removido não é uma sessão.
        var user = await userManager.GetUserAsync(principal);

        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new MeResponse(user.Id, user.Email!));
    }
}
