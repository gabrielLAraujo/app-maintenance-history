using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Infrastructure.Demo;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Api.IntegrationTests.Demo;

[Collection("api")]
public sealed class DemoAccountTests(GarageApiFactory factory)
{
    private const string DemoClaim = "garage:demo";

    [Fact]
    public async Task Created_demo_account_has_no_password_and_expires_in_24_hours()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var demos = scope.ServiceProvider.GetRequiredService<DemoAccountService>();

        var user = await demos.CreateAsync(CancellationToken.None);

        Assert.True(user.IsDemo);
        Assert.Equal(factory.Clock.GetUtcNow().AddHours(24), user.DemoExpiresAt);
        Assert.Null(user.PasswordHash);
        Assert.EndsWith("@demo.garagem.invalid", user.Email, StringComparison.Ordinal);
        Assert.Equal($"demo-{user.Id:N}@demo.garagem.invalid", user.Email);

        // Gravada: o UserManager acha a conta, e ela não aceita senha nenhuma.
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var stored = await users.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(stored);
        Assert.True(stored.IsDemo);
        Assert.False(await users.HasPasswordAsync(stored));
    }

    [Fact]
    public async Task Count_active_ignores_expired_and_real_accounts()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var demos = scope.ServiceProvider.GetRequiredService<DemoAccountService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        // O banco é compartilhado com os outros testes: conta a diferença.
        var before = await demos.CountActiveAsync(CancellationToken.None);

        await demos.CreateAsync(CancellationToken.None);
        var expired = await demos.CreateAsync(CancellationToken.None);
        await demos.ExpireNowAsync(expired.Id, CancellationToken.None);
        var real = new AppUser { UserName = $"real-{Guid.NewGuid():N}@example.com", Email = $"real-{Guid.NewGuid():N}@example.com" };
        Assert.True((await users.CreateAsync(real)).Succeeded);

        Assert.Equal(before + 1, await demos.CountActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Principal_of_a_demo_user_carries_the_demo_claim()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var demos = scope.ServiceProvider.GetRequiredService<DemoAccountService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var principals = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<AppUser>>();
        var demo = await demos.CreateAsync(CancellationToken.None);
        var real = new AppUser { UserName = $"real-{Guid.NewGuid():N}@example.com", Email = $"real-{Guid.NewGuid():N}@example.com" };
        Assert.True((await users.CreateAsync(real)).Succeeded);

        var demoPrincipal = await principals.CreateAsync(demo);
        var realPrincipal = await principals.CreateAsync(real);

        Assert.True(demoPrincipal.HasClaim(DemoClaim, "true"));
        Assert.False(realPrincipal.HasClaim(claim => claim.Type == DemoClaim));
    }

    [Fact]
    public async Task Delete_removes_only_demo_accounts()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var demos = scope.ServiceProvider.GetRequiredService<DemoAccountService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var demo = await demos.CreateAsync(CancellationToken.None);
        var real = new AppUser { UserName = $"real-{Guid.NewGuid():N}@example.com", Email = $"real-{Guid.NewGuid():N}@example.com" };
        Assert.True((await users.CreateAsync(real)).Succeeded);

        await demos.DeleteAsync(demo.Id, CancellationToken.None);
        await demos.DeleteAsync(real.Id, CancellationToken.None);

        await using var check = factory.Services.CreateAsyncScope();
        var fresh = check.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Null(await fresh.FindByIdAsync(demo.Id.ToString()));
        Assert.NotNull(await fresh.FindByIdAsync(real.Id.ToString()));
    }
}
