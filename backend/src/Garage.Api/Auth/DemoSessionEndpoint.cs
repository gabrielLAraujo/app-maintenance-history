using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Demo;
using Garage.Application.Demo.SeedVisitorDemo;
using Garage.Domain.Common;
using Garage.Infrastructure.Demo;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Garage.Api.Auth;

/// <summary>Quando a demo do visitante vence; a sessão vence junto.</summary>
public sealed record DemoSessionResponse(DateTimeOffset ExpiresAt);

/// <summary>
/// <c>POST /auth/demo</c> (spec 3.1): cria uma conta demo sem senha, semeia os dados de exemplo nela e já entra, com uma
/// sessão que vence junto com a conta. Só existe com <c>Demo:Visitors:Enabled</c>.
/// </summary>
internal sealed class DemoSessionEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api)
    {
        if (!api.IsVisitorDemoEnabled())
            return;

        api.MapPost("/auth/demo", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Demo)
            .WithName("StartDemoSession")
            .WithTags("Auth")
            .Produces<DemoSessionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> HandleAsync(
        DemoAccountService demos,
        IOptions<DemoVisitorOptions> options,
        ISender sender,
        SignInManager<AppUser> signInManager,
        CancellationToken ct)
    {
        if (await demos.CountActiveAsync(ct) >= options.Value.MaxActive)
            return DemoErrors.Unavailable.ToProblem();

        var user = await demos.CreateAsync(ct);

        // Nenhuma conta demo vazia fica para trás: se a semeadura falhar, a conta sai antes da resposta.
        Result<DemoSeedResult> seeded;
        try
        {
            seeded = await sender.Send(new SeedVisitorDemoCommand(user.Id), ct);
        }
        catch
        {
            await demos.DeleteAsync(user.Id, CancellationToken.None);
            throw;
        }

        if (seeded.IsFailure)
        {
            await demos.DeleteAsync(user.Id, CancellationToken.None);
            return seeded.Error!.ToProblem();
        }

        // Validade absoluta: a sessão termina quando a conta vence, sem renovação deslizante.
        var expiresAt = user.DemoExpiresAt!.Value;
        await signInManager.SignInAsync(user, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = expiresAt,
            AllowRefresh = false
        });

        return TypedResults.Created("/api/auth/me", new DemoSessionResponse(expiresAt));
    }
}
