using Garage.Application.Abstractions.Ports;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Demo;

/// <summary>
/// Contas de visitante da demo (spec 3): sem senha, com validade, e apagadas com tudo o que é delas (veículos, registros,
/// anexos e links saem em cascata pelo banco). Só mexe em contas com <see cref="AppUser.IsDemo"/>.
/// </summary>
public sealed class DemoAccountService(
    UserManager<AppUser> users,
    GarageDbContext db,
    IAppClock clock,
    IOptions<DemoVisitorOptions> options)
{
    private const string EmailDomain = "demo.garagem.invalid";

    /// <summary>Contas demo ainda válidas (vencimento no futuro).</summary>
    public Task<int> CountActiveAsync(CancellationToken ct)
    {
        var now = clock.Now;
        return db.Users.CountAsync(user => user.IsDemo && user.DemoExpiresAt > now, ct);
    }

    /// <summary>Cria a conta sem senha (ninguém entra nela por login), vencendo em <c>TtlHours</c>.</summary>
    public async Task<AppUser> CreateAsync(CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var email = $"demo-{id:N}@{EmailDomain}";
        // Em segundos inteiros, como a validade do cookie: a resposta, o banco e a sessão dizem o mesmo instante.
        var expiresAt = clock.Now.AddHours(options.Value.TtlHours);
        var user = new AppUser
        {
            Id = id,
            UserName = email,
            Email = email,
            IsDemo = true,
            DemoExpiresAt = expiresAt.AddTicks(-(expiresAt.Ticks % TimeSpan.TicksPerSecond))
        };

        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Não foi possível criar a conta demo: {string.Join("; ", created.Errors.Select(error => error.Code))}");
        }

        return user;
    }

    /// <summary>Marca a conta demo como vencida agora; a próxima limpeza a apaga.</summary>
    public async Task ExpireNowAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.Now;
        await db.Users
            .Where(user => user.Id == userId && user.IsDemo)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DemoExpiresAt, now), ct);
    }

    /// <summary>Apaga a conta demo e, em cascata, tudo o que é dela. Conta real não é tocada.</summary>
    public async Task DeleteAsync(Guid userId, CancellationToken ct) =>
        await db.Users
            .Where(user => user.Id == userId && user.IsDemo)
            .ExecuteDeleteAsync(ct);
}
