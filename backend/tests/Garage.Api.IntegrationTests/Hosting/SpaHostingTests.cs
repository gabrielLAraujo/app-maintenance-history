using System.Net;
using Garage.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Garage.Api.IntegrationTests.Hosting;

[Collection("api")]
public sealed class SpaHostingTests(GarageApiFactory factory) : IDisposable
{
    private const string IndexHtml =
        "<!doctype html><html><head><title>Garagem</title></head><body><div id=\"root\"></div></body></html>";

    private readonly List<string> _tempDirectories = [];

    [Fact]
    public async Task Spa_fallback_serves_index_but_not_for_api()
    {
        using var spa = WithWebRoot();
        using var client = spa.CreateCookieClient();

        using var root = await client.GetAsync("/");
        using var deepLink = await client.GetAsync("/vehicles/6f1c2b9e-0000-0000-0000-000000000000");
        using var publicPage = await client.GetAsync("/p/AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbCde");
        using var unknownApi = await client.GetAsync("/api/naoexiste");

        await AssertSpaIndexAsync(root);
        await AssertSpaIndexAsync(deepLink);
        await AssertSpaIndexAsync(publicPage);
        await AssertNotHtmlAsync(unknownApi, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/naoexiste")]
    [InlineData("/api/vehicles/naoexiste/abc")]
    [InlineData("/openapi/naoexiste")]
    [InlineData("/scalar/naoexiste/abc")]
    [InlineData("/API/naoexiste")]
    public async Task Spa_fallback_never_answers_under_api_openapi_or_scalar(string path)
    {
        using var spa = WithWebRoot();
        using var client = spa.CreateCookieClient();

        using var response = await client.GetAsync(path);

        await AssertNotHtmlAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Spa_fallback_leaves_real_api_and_docs_routes_alone()
    {
        using var spa = WithWebRoot();
        using var client = spa.CreateCookieClient();

        using var health = await client.GetAsync("/api/health");
        using var ready = await client.GetAsync("/api/health/ready");
        using var openApi = await client.GetAsync("/openapi/v1.json");
        using var protectedRoute = await client.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync());
        Assert.Contains("\"openapi\"", await openApi.Content.ReadAsStringAsync());
        await AssertNotHtmlAsync(protectedRoute, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Spa_fallback_does_not_answer_missing_files_or_non_get_requests()
    {
        using var spa = WithWebRoot();
        using var client = spa.CreateCookieClient();

        using var missingAsset = await client.GetAsync("/assets/naoexiste-abc123.js");
        using var missingFile = await client.GetAsync("/robots.txt");
        using var post = await client.PostAsync("/vehicles/abc", content: null);

        await AssertNotHtmlAsync(missingAsset, HttpStatusCode.NotFound);
        await AssertNotHtmlAsync(missingFile, HttpStatusCode.NotFound);
        await AssertNotHtmlAsync(post, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Spa_files_are_served_with_long_cache_only_for_hashed_assets()
    {
        using var spa = WithWebRoot();
        using var client = spa.CreateCookieClient();

        using var asset = await client.GetAsync("/assets/app-4f2a9c.js");
        using var icon = await client.GetAsync("/favicon.svg");
        using var index = await client.GetAsync("/index.html");
        using var fallback = await client.GetAsync("/vehicles/abc");

        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("javascript", asset.Content.Headers.ContentType?.MediaType);
        var assetCache = asset.Headers.CacheControl;
        Assert.NotNull(assetCache);
        Assert.True(assetCache.Public);
        Assert.Equal(TimeSpan.FromDays(365), assetCache.MaxAge);
        Assert.Contains("immutable", assetCache.ToString());

        // O que não tem hash no nome (index, ícone) é revalidado a cada visita, para um deploy novo aparecer na hora.
        foreach (var response in new[] { icon, index, fallback })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoCache, $"Sem no-cache em {response.RequestMessage?.RequestUri}.");
        }
    }

    [Fact]
    public async Task Spa_fallback_under_assets_is_revalidated_never_immutable()
    {
        using var spa = WithWebRoot();
        using var client = spa.CreateCookieClient();

        // Sem extensão, /assets/foo não é arquivo do build: cai no fallback, que serve o index.html.
        using var response = await client.GetAsync("/assets/foo");

        await AssertSpaIndexAsync(response);
        var cache = response.Headers.CacheControl;
        Assert.NotNull(cache);
        Assert.True(cache.NoCache);
        Assert.DoesNotContain("immutable", cache.ToString(), StringComparison.Ordinal);
        Assert.Null(cache.MaxAge);
    }

    [Fact]
    public async Task Public_page_is_not_indexed_but_the_rest_of_the_spa_is()
    {
        using var spa = WithWebRoot();
        using var client = spa.CreateCookieClient();

        using var publicPage = await client.GetAsync("/p/AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbCde");
        using var deepLink = await client.GetAsync("/vehicles/abc");
        using var root = await client.GetAsync("/");

        await AssertSpaIndexAsync(publicPage);
        Assert.Equal(["noindex"], publicPage.Headers.GetValues("X-Robots-Tag"));
        foreach (var response in new[] { deepLink, root })
        {
            await AssertSpaIndexAsync(response);
            Assert.False(response.Headers.Contains("X-Robots-Tag"), $"X-Robots-Tag em {response.RequestMessage?.RequestUri}.");
        }
    }

    [Fact]
    public async Task Spa_fallback_is_not_mapped_without_index_html()
    {
        using var empty = WithWebRoot(withIndex: false);
        using var client = empty.CreateCookieClient();

        using var root = await client.GetAsync("/");
        using var deepLink = await client.GetAsync("/vehicles/abc");
        using var health = await client.GetAsync("/api/health");

        await AssertNotHtmlAsync(root, HttpStatusCode.NotFound);
        await AssertNotHtmlAsync(deepLink, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Data_protection_keys_are_persisted_to_configured_path()
    {
        var keysPath = NewTempDirectory();
        using var persisted = factory.WithWebHostBuilder(builder => builder.UseSetting("DataProtection:KeysPath", keysPath));
        using var client = persisted.CreateCookieClient();

        using var response = await client.GetAsync("/api/auth/antiforgery");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotEmpty(Directory.GetFiles(keysPath, "key-*.xml"));
    }

    [Fact]
    public void Data_protection_application_name_is_stable()
    {
        var options = factory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

        Assert.Equal("garage", options.ApplicationDiscriminator);
    }

    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Limpeza de temporários: um arquivo ainda aberto pelo host de teste não deve reprovar o teste.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Host de teste com um <c>wwwroot</c> temporário: com <paramref name="withIndex"/>, um build de front em miniatura.</summary>
    private WebApplicationFactory<Program> WithWebRoot(bool withIndex = true)
    {
        var webRoot = NewTempDirectory();
        if (withIndex)
        {
            File.WriteAllText(Path.Combine(webRoot, "index.html"), IndexHtml);
            File.WriteAllText(Path.Combine(webRoot, "favicon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            Directory.CreateDirectory(Path.Combine(webRoot, "assets"));
            File.WriteAllText(Path.Combine(webRoot, "assets", "app-4f2a9c.js"), "console.log('garagem');");
        }

        return factory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot));
    }

    private string NewTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "garage-tests", "spa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }

    private static async Task AssertSpaIndexAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected the SPA index but got {(int)response.StatusCode}: {body}");
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<div id=\"root\">", body);
    }

    private static async Task AssertNotHtmlAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status,
            $"Expected {(int)status} {status} but got {(int)response.StatusCode} {response.StatusCode}: {body}");
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("<div id=\"root\">", body);
    }
}
