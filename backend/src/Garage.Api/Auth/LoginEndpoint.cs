using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Garage.Api.Auth;

internal sealed class LoginEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/auth/login", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithName("Login")
            .WithTags("Auth")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(LoginRequest request, SignInManager<AppUser> signInManager)
    {
        // O nome de usuário é o e-mail. Cada erro de senha conta para o bloqueio (5 tentativas, 15 minutos).
        var result = await signInManager.PasswordSignInAsync(
            (request.Email ?? string.Empty).Trim(), request.Password ?? string.Empty, isPersistent: true, lockoutOnFailure: true);

        if (result.Succeeded)
            return TypedResults.NoContent();

        return (result.IsLockedOut ? ApiErrors.LockedOut : ApiErrors.InvalidCredentials).ToProblem();
    }
}
