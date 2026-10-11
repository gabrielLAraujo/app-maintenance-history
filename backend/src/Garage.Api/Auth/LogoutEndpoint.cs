using Garage.Api.Endpoints;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Garage.Api.Auth;

internal sealed class LogoutEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/auth/logout", HandleAsync)
            .WithName("Logout")
            .WithTags("Auth")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(SignInManager<AppUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }
}
