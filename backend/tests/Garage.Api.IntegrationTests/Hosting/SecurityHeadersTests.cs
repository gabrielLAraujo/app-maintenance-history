using System.Net;
using Garage.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Api.IntegrationTests.Hosting;

[Collection("api")]
public sealed class SecurityHeadersTests(GarageApiFactory factory)
{
    private const string FailingPath = "/api/test-falha";

    [Fact]
    public async Task Every_response_has_the_security_headers_even_errors()
    {
        using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter, FailingRoute>()));
        using var client = failing.CreateCookieClient();

        using var ok = await client.GetAsync("/api/health");
        using var notFound = await client.GetAsync("/api/naoexiste");
        using var unauthorized = await client.GetAsync("/api/vehicles");
        using var serverError = await client.GetAsync(FailingPath);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, serverError.StatusCode);
        foreach (var response in new[] { ok, notFound, unauthorized, serverError })
        {
            AssertHeader(response, "X-Content-Type-Options", "nosniff");
            AssertHeader(response, "X-Frame-Options", "DENY");
            AssertHeader(response, "Referrer-Policy", "no-referrer");
            // O cliente fala HTTPS (como o navegador atrás do Traefik): o HSTS vale também nas respostas de erro.
            AssertHeader(response, "Strict-Transport-Security", "max-age=31536000");
        }
    }

    [Fact]
    public async Task Hsts_is_sent_only_over_https()
    {
        using var client = factory.CreateCookieClient("http://localhost");

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.Equal(["DENY"], response.Headers.GetValues("X-Frame-Options"));
    }

    [Fact]
    public async Task Hsts_is_not_sent_in_development()
    {
        using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateCookieClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.Equal(["no-referrer"], response.Headers.GetValues("Referrer-Policy"));
    }

    private static void AssertHeader(HttpResponseMessage response, string name, string expected)
    {
        string[] values = response.Headers.TryGetValues(name, out var found) ? found.ToArray() : [];
        Assert.True(values is [var single] && single == expected,
            $"{name} em {response.RequestMessage?.RequestUri}: esperado '{expected}', veio [{string.Join(", ", values)}].");
    }

    /// <summary>
    /// Uma rota que lança exceção, para exercitar o 500 do handler global. Entra depois dos endpoints, dentro do pipeline
    /// da API (e portanto do <c>UseExceptionHandler</c>): só responde quando nenhum endpoint casou com o caminho.
    /// </summary>
    private sealed class FailingRoute : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((context, nextMiddleware) => context.Request.Path == FailingPath
                ? throw new InvalidOperationException("falha de teste")
                : nextMiddleware(context));
        };
    }
}
