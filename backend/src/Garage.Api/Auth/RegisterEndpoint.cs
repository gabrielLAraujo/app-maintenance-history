using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Domain.Common;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Garage.Api.Auth;

internal sealed class RegisterEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api)
    {
        if (!api.IsRegistrationEnabled())
            return;

        api.MapPost("/auth/register", HandleAsync)
            .AllowAnonymous()
            // Mesma cota do login, por IP: sem ela o cadastro aberto aceitaria contas (e hashes de senha) sem limite.
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithName("Register")
            .WithTags("Auth")
            .Produces<CreatedIdResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> HandleAsync(
        RegisterRequest request, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
    {
        var email = (request.Email ?? string.Empty).Trim();
        var user = new AppUser { Id = Guid.CreateVersion7(), UserName = email, Email = email };

        var result = await userManager.CreateAsync(user, request.Password ?? string.Empty);
        if (!result.Succeeded)
            return Error.Validation(ToFieldErrors(result.Errors)).ToProblem();

        await signInManager.SignInAsync(user, isPersistent: true);
        return TypedResults.Created("/api/auth/me", new CreatedIdResponse(user.Id));
    }

    private static Dictionary<string, string[]> ToFieldErrors(IEnumerable<IdentityError> errors) =>
        errors.Select(ToFieldError)
            .GroupBy(error => error.Field)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).Distinct().ToArray());

    private static (string Field, string Message) ToFieldError(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort) => ("password", "A senha deve ter pelo menos 10 caracteres."),
        nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)
            => ("email", "E-mail já cadastrado."),
        nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName)
            => ("email", "E-mail inválido."),
        _ => ("password", "Senha inválida.")
    };
}
