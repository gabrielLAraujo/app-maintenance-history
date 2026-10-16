using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.Cli;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Infrastructure.Demo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Garage.Api.IntegrationTests.Demo;

[Collection("api")]
public sealed class DemoRestrictionTests(GarageApiFactory factory)
{
    // Mesmo motivo de DemoSessionTests: a sessão da demo vence em 24 h pelo relógio do host, e o HttpClient descarta cookie
    // vencido pelo relógio real.
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

    [Fact]
    public async Task Demo_upload_is_403()
    {
        using var host = factory.WithClock(_clock);
        using var demo = await StartDemoAsync(host);
        var vehicleId = (await VehiclesAsync(demo)).First().GetProperty("id").GetGuid();
        var recordId = (await demo.GetRecordsAsync(vehicleId)).GetProperty("items")[0].GetProperty("id").GetGuid();

        using var response = await demo.PostAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "a.png");

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, "demo.uploads_disabled");
    }

    [Fact]
    public async Task Demo_vehicle_limit_is_enforced()
    {
        using var host = factory.WithClock(_clock)
            .WithWebHostBuilder(builder => builder.UseSetting("Demo:Visitors:MaxVehicles", "3"));
        using var demo = await StartDemoAsync(host);

        await demo.CreateVehicleAsync();
        using var fourth = await demo.PostAsJsonAsync("/api/vehicles", TestData.Vehicle(), TestJson.Options);

        var problem = await fourth.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "demo.vehicle_limit_reached");
        Assert.Equal(3, problem.GetProperty("max").GetInt32());
        Assert.Equal("A demonstração permite até 3 veículos.", problem.GetProperty("detail").GetString());
        Assert.Equal(3, (await VehiclesAsync(demo)).Count);
    }

    [Fact]
    public async Task Vehicle_limit_holds_under_concurrent_requests()
    {
        using var host = factory.WithClock(_clock);
        using var demo = await StartDemoAsync(host);

        // A demo já tem 2 veículos e o teto é 5: oito pedidos ao mesmo tempo criam só 3.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => demo.PostAsJsonAsync("/api/vehicles", TestData.Vehicle(), TestJson.Options)));

        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, (await VehiclesAsync(demo)).Count);
        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task Demo_writes_are_rate_limited_per_account()
    {
        using var host = factory.WithClock(_clock)
            .WithWebHostBuilder(builder => builder.UseSetting("RateLimits:DemoWritesPerHour", "2"));
        using var demo = await StartDemoAsync(host);

        await demo.CreateVehicleAsync();
        await demo.CreateVehicleAsync();
        using var third = await demo.PostAsJsonAsync("/api/vehicles", TestData.Vehicle(), TestJson.Options);

        await third.AssertProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
        // Ler continua liberado, e a conta real não entra nessa cota.
        Assert.Equal(4, (await VehiclesAsync(demo)).Count);
        using var real = await host.CreateUserClientAsync();
        for (var i = 0; i < 3; i++)
            await real.CreateVehicleAsync();
    }

    [Fact]
    public async Task Real_accounts_are_not_limited()
    {
        using var owner = await factory.CreateUserClientAsync();

        Guid vehicleId = default;
        for (var i = 0; i < 6; i++)
            vehicleId = await owner.CreateVehicleAsync();
        var recordId = await owner.CreateRecordAsync(vehicleId);
        await owner.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "a.png");

        Assert.Equal(6, (await VehiclesAsync(owner)).Count);
    }

    [Fact]
    public async Task Reset_password_cli_refuses_a_demo_account()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var demo = await scope.ServiceProvider.GetRequiredService<DemoAccountService>().CreateAsync(CancellationToken.None);
        var output = new StringWriter();

        var exitCode = await ResetPasswordCli.RunAsync(factory.Services, demo.Email!, output);

        Assert.Equal(1, exitCode);
        Assert.Equal("Conta de demonstração não tem senha.", output.ToString().Trim());
    }

    private static async Task<HttpClient> StartDemoAsync(WebApplicationFactory<Program> host)
    {
        var client = await host.CreateAnonymousClientAsync();
        using var response = await client.PostAsync("/api/auth/demo", content: null);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Demo failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        await client.RefreshAntiforgeryAsync();
        return client;
    }

    private static async Task<List<JsonElement>> VehiclesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/vehicles", TestJson.Options)).EnumerateArray().ToList();
}
