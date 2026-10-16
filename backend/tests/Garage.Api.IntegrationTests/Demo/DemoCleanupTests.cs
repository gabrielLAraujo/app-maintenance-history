using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Domain.Vehicles;
using Garage.Infrastructure.Demo;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Garage.Api.IntegrationTests.Demo;

[Collection("api")]
public sealed class DemoCleanupTests(GarageApiFactory factory)
{
    private const int BatchSize = 50;

    // Relógio próprio no "agora" real (ver DemoSessionTests), que estes testes avançam.
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

    [Fact]
    public async Task Expired_demo_is_removed_with_its_data_and_files()
    {
        using var host = factory.WithClock(_clock);
        // O banco é compartilhado: primeiro some com as demos que outros testes deixaram, para contar só a deste.
        _clock.Advance(TimeSpan.FromHours(25));
        while ((await PurgeAsync(host)).Accounts == BatchSize)
        {
        }

        var (demo, userId) = await StartDemoAsync(host);
        var vehicleIds = (await demo.GetFromJsonAsync<JsonElement>("/api/vehicles", TestJson.Options))
            .EnumerateArray().Select(vehicle => vehicle.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(2, vehicleIds.Count);
        Assert.All(vehicleIds, id => Assert.NotEmpty(FilesOf(id)));

        _clock.Advance(TimeSpan.FromHours(25));
        var purged = await PurgeAsync(host);

        // 9 arquivos do Civic (8 fotos e a nota fiscal) e 1 do Gol.
        Assert.Equal(1, purged.Accounts);
        Assert.Equal(10, purged.Files);
        Assert.False(await UserExistsAsync(host, userId));
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageDbContext>();
        Assert.False(await db.Set<Vehicle>().AnyAsync(vehicle => vehicle.OwnerId == userId));
        Assert.All(vehicleIds, id => Assert.Empty(FilesOf(id)));
    }

    [Fact]
    public async Task Valid_demo_and_real_accounts_stay()
    {
        using var host = factory.WithClock(_clock);
        var (_, demoId) = await StartDemoAsync(host);
        using var real = await host.CreateUserClientAsync();
        var realId = (await real.GetFromJsonAsync<JsonElement>("/api/auth/me", TestJson.Options)).GetProperty("id").GetGuid();

        _clock.Advance(TimeSpan.FromHours(1));
        await PurgeAsync(host);

        Assert.True(await UserExistsAsync(host, demoId));
        Assert.True(await UserExistsAsync(host, realId));
    }

    [Fact]
    public async Task Demo_expired_by_logout_is_removed_on_the_next_run()
    {
        using var host = factory.WithClock(_clock);
        var (demo, userId) = await StartDemoAsync(host);
        using (var logout = await demo.PostAsync("/api/auth/logout", content: null))
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        await PurgeAsync(host);

        Assert.False(await UserExistsAsync(host, userId));
    }

    private static async Task<DemoPurgeResult> PurgeAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DemoAccountService>().PurgeExpiredAsync(BatchSize, CancellationToken.None);
    }

    private static async Task<bool> UserExistsAsync(WebApplicationFactory<Program> host, Guid userId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByIdAsync(userId.ToString()) is not null;
    }

    private static async Task<(HttpClient Client, Guid UserId)> StartDemoAsync(WebApplicationFactory<Program> host)
    {
        var client = await host.CreateAnonymousClientAsync();
        using var response = await client.PostAsync("/api/auth/demo", content: null);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Demo failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        await client.RefreshAntiforgeryAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", TestJson.Options);
        return (client, me.GetProperty("id").GetGuid());
    }

    /// <summary>Os arquivos guardados do veículo (as chaves são <c>{vehicleId}/{attachmentId}</c>).</summary>
    private string[] FilesOf(Guid vehicleId)
    {
        var directory = Path.Combine(factory.StoragePath, vehicleId.ToString());
        return Directory.Exists(directory) ? Directory.GetFiles(directory) : [];
    }
}
