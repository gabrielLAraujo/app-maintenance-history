using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Garage.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Clientes HTTP para os testes. São extensões sobre <see cref="WebApplicationFactory{TEntryPoint}"/> para funcionar
/// também nas factories derivadas de <c>WithWebHostBuilder</c>.
/// </summary>
public static class TestClients
{
    public const string Password = "senha-de-teste-123";

    private const string AntiforgeryHeader = "X-XSRF-TOKEN";
    private const string AntiforgeryCookie = "XSRF-TOKEN";

    /// <summary>Cliente com cookies habilitados e <c>https://localhost</c> como base (os cookies são <c>Secure</c>).</summary>
    public static HttpClient CreateCookieClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    /// <summary>Cliente sem sessão, já com o <c>X-XSRF-TOKEN</c> anônimo.</summary>
    public static async Task<HttpClient> CreateAnonymousClientAsync(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateCookieClient();
        await client.RefreshAntiforgeryAsync();
        return client;
    }

    /// <summary>Registra um usuário novo, deixa o cliente autenticado e com o <c>X-XSRF-TOKEN</c> vinculado a ele.</summary>
    public static async Task<HttpClient> CreateUserClientAsync(this WebApplicationFactory<Program> factory, string? email = null)
    {
        var client = await factory.CreateAnonymousClientAsync();

        email ??= $"user-{Guid.NewGuid():N}@example.com";
        using var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password }, TestJson.Options);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Register failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        await client.RefreshAntiforgeryAsync();
        return client;
    }

    /// <summary>Busca um token novo em <c>/api/auth/antiforgery</c> e o põe no cabeçalho padrão do cliente.</summary>
    public static async Task RefreshAntiforgeryAsync(this HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var setCookie = response.FindSetCookie(AntiforgeryCookie);
        var token = setCookie[(AntiforgeryCookie.Length + 1)..].Split(';')[0];

        client.DefaultRequestHeaders.Remove(AntiforgeryHeader);
        client.DefaultRequestHeaders.Add(AntiforgeryHeader, Uri.UnescapeDataString(token));
    }

    /// <summary>A linha <c>Set-Cookie</c> completa do cookie <paramref name="name"/>.</summary>
    public static string FindSetCookie(this HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values), "A resposta não define cookies.");
        return Assert.Single(values!, value => value.StartsWith(name + "=", StringComparison.Ordinal));
    }
}
