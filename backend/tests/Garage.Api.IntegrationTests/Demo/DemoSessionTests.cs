using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Garage.Api.IntegrationTests.Demo;

[Collection("api")]
public sealed class DemoSessionTests(GarageApiFactory factory)
{
    private const string DemoUrl = "/api/auth/demo";
    private const string AuthCookie = "garage_auth";

    // O HttpClient descarta cookie vencido pelo relógio real, e o relógio da fábrica está no passado (2026-10-10): a sessão
    // da demo, que vence em 24 h, já chegaria vencida. Cada teste usa um host com relógio próprio no "agora" real.
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Demo_session_logs_in_with_the_example_data()
    {
        using var host = factory.WithClock(_clock);
        using var client = await host.CreateAnonymousClientAsync();

        using var response = await client.PostAsync(DemoUrl, content: null);

        var body = await AssertCreatedAsync(response);
        Assert.Equal(_clock.GetUtcNow().AddHours(24), body.GetProperty("expiresAt").GetDateTimeOffset());
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", TestJson.Options);
        Assert.True(me.GetProperty("isDemo").GetBoolean());
        Assert.Equal(body.GetProperty("expiresAt").GetDateTimeOffset(), me.GetProperty("demoExpiresAt").GetDateTimeOffset());
        Assert.Equal(2, await VehicleCountAsync(client));
    }

    [Fact]
    public async Task Two_demo_sessions_do_not_see_each_other()
    {
        using var host = factory.WithClock(_clock);
        using var first = await StartDemoAsync(host);
        using var second = await StartDemoAsync(host);

        await first.CreateVehicleAsync();

        Assert.Equal(3, await VehicleCountAsync(first));
        Assert.Equal(2, await VehicleCountAsync(second));
    }

    [Fact]
    public async Task Demo_account_cannot_log_in_with_a_password()
    {
        using var host = factory.WithClock(_clock);
        using var demo = await StartDemoAsync(host);
        var email = (await demo.GetFromJsonAsync<JsonElement>("/api/auth/me", TestJson.Options)).GetProperty("email").GetString();
        using var visitor = await host.CreateAnonymousClientAsync();

        using var login = await visitor.PostAsJsonAsync(
            "/api/auth/login", new { email, password = TestClients.Password }, TestJson.Options);

        await login.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
    }

    [Fact]
    public async Task Demo_is_503_when_max_active_is_reached()
    {
        using var full = factory.WithWebHostBuilder(builder => builder.UseSetting("Demo:Visitors:MaxActive", "0"));
        using var client = await full.CreateAnonymousClientAsync();

        using var response = await client.PostAsync(DemoUrl, content: null);

        await response.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "demo.unavailable");
    }

    [Fact]
    public async Task Demo_is_rate_limited_per_ip()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:DemoPerHour", "1"));
        using var client = await limited.CreateAnonymousClientAsync();

        using var allowed = await client.PostAsync(DemoUrl, content: null);
        await AssertCreatedAsync(allowed);
        using var visitor = await limited.CreateAnonymousClientAsync();
        using var limitedResponse = await visitor.PostAsync(DemoUrl, content: null);

        await limitedResponse.AssertProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
    }

    [Fact]
    public async Task Demo_is_404_when_disabled()
    {
        using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Demo:Visitors:Enabled", "false"));
        using var client = await disabled.CreateAnonymousClientAsync();

        using var response = await client.PostAsync(DemoUrl, content: null);
        var config = await client.GetFromJsonAsync<JsonElement>("/api/auth/config", TestJson.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(config.GetProperty("demoEnabled").GetBoolean());
    }

    [Fact]
    public async Task Logout_expires_the_demo_and_the_old_cookie_stops_working()
    {
        using var host = factory.WithClock(_clock);
        using var client = await host.CreateAnonymousClientAsync();
        using var started = await client.PostAsync(DemoUrl, content: null);
        await AssertCreatedAsync(started);
        var oldCookies = AuthCookies(started);
        await client.RefreshAntiforgeryAsync();

        using (var logout = await client.PostAsync("/api/auth/logout", content: null))
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Quem guardou o cookie de antes do logout não continua na demo.
        using var replay = host.CreateCookieClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", oldCookies);
        using var me = await replay.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    private static async Task<HttpClient> StartDemoAsync(WebApplicationFactory<Program> host)
    {
        var client = await host.CreateAnonymousClientAsync();
        using var response = await client.PostAsync(DemoUrl, content: null);
        await AssertCreatedAsync(response);
        // O token do antiforgery é vinculado ao usuário: o anônimo não vale depois de entrar.
        await client.RefreshAntiforgeryAsync();
        return client;
    }

    private static async Task<JsonElement> AssertCreatedAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Demo failed: {(int)response.StatusCode} {content}");
        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options);
    }

    private static async Task<int> VehicleCountAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/vehicles", TestJson.Options)).GetArrayLength();

    /// <summary>O cookie de sessão (e seus pedaços, se o Identity o dividir) como valor de um cabeçalho <c>Cookie</c>.</summary>
    private static string AuthCookies(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values), "A resposta não define cookies.");
        var pairs = values!
            .Where(value => value.StartsWith(AuthCookie, StringComparison.Ordinal))
            .Select(value => value.Split(';')[0])
            .ToList();
        Assert.NotEmpty(pairs);
        return string.Join("; ", pairs);
    }
}
