using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.Cli;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Api.IntegrationTests.Auth;

[Collection("api")]
public sealed class AuthTests(GarageApiFactory factory)
{
    private const string WrongPassword = "senha-errada-123";

    private static readonly string[] AnonymousRoutes =
    [
        "/api/health", "/api/health/ready", "/api/auth/config", "/api/auth/antiforgery", "/api/auth/register", "/api/auth/login"
    ];

    [Fact]
    public async Task Config_reports_registration_enabled()
    {
        using var client = await factory.CreateAnonymousClientAsync();

        var response = await client.GetAsync("/api/auth/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        Assert.True(body.GetProperty("registrationEnabled").GetBoolean());
    }

    [Fact]
    public async Task Register_then_me_returns_user()
    {
        var email = NewEmail();
        using var client = await factory.CreateUserClientAsync(email);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        Assert.NotEqual(Guid.Empty, me.GetProperty("id").GetGuid());
        Assert.Equal(email, me.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_with_short_password_returns_400_with_password_error()
    {
        using var client = await factory.CreateAnonymousClientAsync();

        using var response = await PostCredentialsAsync(client, "/api/auth/register", NewEmail(), "123456789");

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal("A senha deve ter pelo menos 10 caracteres.", problem.GetProperty("errors").GetProperty("password")[0].GetString());
    }

    [Fact]
    public async Task Register_duplicate_email_returns_400_with_email_error()
    {
        var email = NewEmail();
        using var existing = await factory.CreateUserClientAsync(email);
        using var client = await factory.CreateAnonymousClientAsync();

        using var response = await PostCredentialsAsync(client, "/api/auth/register", email.ToUpperInvariant(), TestClients.Password);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal("E-mail já cadastrado.", problem.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_invalid_credentials()
    {
        var email = NewEmail();
        using var existing = await factory.CreateUserClientAsync(email);
        using var client = await factory.CreateAnonymousClientAsync();

        using var response = await PostCredentialsAsync(client, "/api/auth/login", email, WrongPassword);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
    }

    [Fact]
    public async Task Login_and_logout_round_trip()
    {
        var email = NewEmail();
        using var registered = await factory.CreateUserClientAsync(email);
        using var client = await factory.CreateAnonymousClientAsync();

        using var login = await PostCredentialsAsync(client, "/api/auth/login", email, TestClients.Password);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        await client.RefreshAntiforgeryAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        using var logout = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        var email = NewEmail();
        using var existing = await factory.CreateUserClientAsync(email);
        using var client = await factory.CreateAnonymousClientAsync();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var wrong = await PostCredentialsAsync(client, "/api/auth/login", email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var response = await PostCredentialsAsync(client, "/api/auth/login", email, TestClients.Password);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.locked_out");
    }

    [Fact]
    public async Task Mutating_request_without_antiforgery_header_returns_400()
    {
        using var client = await factory.CreateUserClientAsync();
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        using var response = await client.PostAsync("/api/auth/logout", content: null);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "auth.antiforgery_invalid");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Antiforgery_token_issued_before_login_is_rejected_afterwards()
    {
        using var client = await factory.CreateAnonymousClientAsync();
        using var register = await PostCredentialsAsync(client, "/api/auth/register", NewEmail(), TestClients.Password);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var response = await client.PostAsync("/api/auth/logout", content: null);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "auth.antiforgery_invalid");
    }

    [Fact]
    public async Task Antiforgery_endpoint_sets_a_script_readable_xsrf_cookie()
    {
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync("/api/auth/antiforgery");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = response.FindSetCookie("XSRF-TOKEN");
        Assert.DoesNotContain("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Antiforgery_endpoint_works_over_plain_http()
    {
        using var client = factory.CreateCookieClient("http://localhost");

        using var response = await client.GetAsync("/api/auth/antiforgery");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Antiforgery_validation_works_over_plain_http()
    {
        using var client = factory.CreateCookieClient("http://localhost");
        await client.RefreshAntiforgeryAsync();

        // Com o token o POST chega ao handler (401 de credenciais), em vez de 500 na validação ou 400 de antiforgery.
        using var withToken = await PostCredentialsAsync(client, "/api/auth/login", NewEmail(), WrongPassword);
        await withToken.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        using var withoutToken = await PostCredentialsAsync(client, "/api/auth/login", NewEmail(), WrongPassword);
        await withoutToken.AssertProblemAsync(HttpStatusCode.BadRequest, "auth.antiforgery_invalid");
    }

    [Fact]
    public async Task Antiforgery_cookies_are_secure_behind_a_tls_terminating_proxy()
    {
        using var client = factory.CreateCookieClient("http://localhost");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/antiforgery");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("secure", response.FindSetCookie("XSRF-TOKEN"), StringComparison.OrdinalIgnoreCase);
        // O cookie do antiforgery (nome com sufixo gerado) segue o esquema da requisição: seguro só por causa do X-Forwarded-Proto.
        var antiforgeryCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        Assert.Contains("secure", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_without_cookie_returns_401_not_redirect()
    {
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Auth_cookie_is_httponly_secure_strict()
    {
        using var client = await factory.CreateAnonymousClientAsync();

        using var response = await PostCredentialsAsync(client, "/api/auth/register", NewEmail(), TestClients.Password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cookie = response.FindSetCookie("garage_auth");
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Responses_carry_nosniff_header()
    {
        using var client = factory.CreateCookieClient();

        foreach (var path in new[] { "/api/auth/config", "/api/auth/me" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        }
    }

    [Fact]
    public async Task Register_is_404_when_registration_disabled()
    {
        using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Auth:AllowRegistration", "false"));
        using var client = await disabled.CreateAnonymousClientAsync();

        using var register = await PostCredentialsAsync(client, "/api/auth/register", NewEmail(), TestClients.Password);
        var config = await client.GetFromJsonAsync<JsonElement>("/api/auth/config", TestJson.Options);

        Assert.Equal(HttpStatusCode.NotFound, register.StatusCode);
        Assert.False(config.GetProperty("registrationEnabled").GetBoolean());
    }

    [Fact]
    public async Task Login_is_rate_limited()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:LoginPerMinute", "2"));
        using var client = await limited.CreateAnonymousClientAsync();
        var email = NewEmail();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            using var allowed = await PostCredentialsAsync(client, "/api/auth/login", email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var response = await PostCredentialsAsync(client, "/api/auth/login", email, WrongPassword);

        await response.AssertProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
    }

    [Fact]
    public async Task Login_rate_limit_is_per_client_ip_from_forwarded_headers()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:LoginPerMinute", "1"));
        using var client = await limited.CreateAnonymousClientAsync();

        using var first = await LoginFromAsync(client, "203.0.113.10");
        using var second = await LoginFromAsync(client, "203.0.113.10");
        using var otherClient = await LoginFromAsync(client, "203.0.113.20");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        await second.AssertProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
        Assert.Equal(HttpStatusCode.Unauthorized, otherClient.StatusCode);
    }

    [Fact]
    public async Task Password_reset_issues_working_temporary_password_and_unlocks()
    {
        var email = NewEmail();
        using var existing = await factory.CreateUserClientAsync(email);
        using var client = await factory.CreateAnonymousClientAsync();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var wrong = await PostCredentialsAsync(client, "/api/auth/login", email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var locked = await PostCredentialsAsync(client, "/api/auth/login", email, TestClients.Password);
        await locked.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.locked_out");

        await using var scope = factory.Services.CreateAsyncScope();
        var temporary = await scope.ServiceProvider.GetRequiredService<PasswordResetService>().ResetAsync(email, CancellationToken.None);

        Assert.NotNull(temporary);
        Assert.Equal(16, temporary.Length);
        using var login = await PostCredentialsAsync(client, "/api/auth/login", email, temporary);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }

    [Fact]
    public async Task Password_reset_for_unknown_email_returns_null()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var temporary = await scope.ServiceProvider.GetRequiredService<PasswordResetService>()
            .ResetAsync(NewEmail(), CancellationToken.None);

        Assert.Null(temporary);
    }

    [Fact]
    public async Task Reset_password_cli_prints_a_working_temporary_password()
    {
        var email = NewEmail();
        using var existing = await factory.CreateUserClientAsync(email);
        var output = new StringWriter();

        var exitCode = await ResetPasswordCli.RunAsync(factory.Services, email, output);

        Assert.Equal(0, exitCode);
        const string prefix = "Senha temporária: ";
        var line = output.ToString().Trim();
        Assert.StartsWith(prefix, line);
        using var client = await factory.CreateAnonymousClientAsync();
        using var login = await PostCredentialsAsync(client, "/api/auth/login", email, line[prefix.Length..]);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }

    [Fact]
    public async Task Reset_password_cli_reports_unknown_user()
    {
        var output = new StringWriter();

        var exitCode = await ResetPasswordCli.RunAsync(factory.Services, NewEmail(), output);

        Assert.Equal(1, exitCode);
        Assert.Equal("Usuário não encontrado.", output.ToString().Trim());
    }

    [Fact]
    public void Every_api_endpoint_requires_authorization_except_allowlist()
    {
        var apiEndpoints = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .ToList();

        var open = apiEndpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
                               || endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Distinct()
            .ToList();
        var protectedRoutes = apiEndpoints.Select(endpoint => endpoint.RoutePattern.RawText!).Except(open).ToList();

        var unexpected = open.Where(route => !IsAllowedAnonymous(route)).ToList();
        Assert.True(unexpected.Count == 0, $"Rotas sem autorização fora da allowlist: {string.Join(", ", unexpected)}");
        Assert.Contains("/api/auth/me", protectedRoutes);
        Assert.Contains("/api/auth/logout", protectedRoutes);
    }

    private static bool IsAllowedAnonymous(string route) =>
        AnonymousRoutes.Contains(route) || route.StartsWith("/api/public/", StringComparison.Ordinal);

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = NewEmail(), password = WrongPassword }, options: TestJson.Options)
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostCredentialsAsync(HttpClient client, string url, string email, string password) =>
        client.PostAsJsonAsync(url, new { email, password }, TestJson.Options);
}
