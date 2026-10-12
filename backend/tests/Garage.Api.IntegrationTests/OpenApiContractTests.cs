using System.Net;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Garage.Api.IntegrationTests;

/// <summary>
/// O <c>/openapi/v1.json</c> é a base do front (os tipos de <c>src/api/schema.d.ts</c> saem dele): estes testes impedem que
/// um endpoint novo entre no documento sem nome, tag ou resposta de sucesso com schema.
/// </summary>
[Collection("api")]
public sealed class OpenApiContractTests(GarageApiFactory factory) : IDisposable
{
    private static readonly string[] HttpMethods = ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

    // Rotas que entregam os bytes de um arquivo (imagem ou PDF), não JSON: o contrato delas é o tipo de mídia.
    private static readonly string[] FileStreamPaths =
    [
        "/api/vehicles/{vehicleId}/attachments/{attachmentId}/content",
        "/api/public/{token}/attachments/{attachmentId}"
    ];

    private readonly List<string> _tempDirectories = [];

    [Fact]
    public async Task Every_endpoint_declares_success_response_with_schema()
    {
        using var document = await LoadDocumentAsync(factory.CreateClient());
        var operations = Operations(document.RootElement).ToList();
        Assert.NotEmpty(operations);
        var failures = new List<string>();

        foreach (var (method, path, operation) in operations)
        {
            var name = $"{method} {path}";
            var successes = operation.GetProperty("responses").EnumerateObject()
                .Where(response => response.Name.StartsWith('2'))
                .ToList();
            if (successes.Count == 0)
                failures.Add($"{name}: sem resposta 2xx.");

            foreach (var response in successes)
            {
                var hasContent = response.Value.TryGetProperty("content", out var content);

                if (response.Name == "204")
                {
                    if (hasContent)
                        failures.Add($"{name}: a resposta 204 não pode ter corpo.");
                }
                else if (FileStreamPaths.Contains(path))
                {
                    if (!hasContent)
                        failures.Add($"{name}: a resposta {response.Name} (arquivo) não declara o tipo de mídia.");
                }
                else if (!hasContent || !content.TryGetProperty("application/json", out var json) || !HasSchema(json))
                {
                    failures.Add($"{name}: a resposta {response.Name} não declara application/json com schema.");
                }
            }
        }

        AssertNoFailures(failures);
    }

    [Fact]
    public async Task Every_endpoint_has_a_unique_operation_id_and_a_tag()
    {
        using var document = await LoadDocumentAsync(factory.CreateClient());
        var failures = new List<string>();
        var operationIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (method, path, operation) in Operations(document.RootElement))
        {
            var name = $"{method} {path}";

            if (!operation.TryGetProperty("operationId", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
                failures.Add($"{name}: sem operationId (.WithName).");
            else if (!operationIds.Add(id.GetString()!))
                failures.Add($"{name}: operationId '{id.GetString()}' repetido.");

            if (!operation.TryGetProperty("tags", out var tags) || tags.GetArrayLength() == 0)
                failures.Add($"{name}: sem tag (.WithTags).");
        }

        AssertNoFailures(failures);
    }

    [Theory]
    [InlineData("/api/vehicles")]
    [InlineData("/api/vehicles/{vehicleId}/records")]
    [InlineData("/api/vehicles/{vehicleId}/reminders")]
    [InlineData("/api/reminders/due")]
    [InlineData("/api/vehicles/{vehicleId}/share-links")]
    [InlineData("/api/public/{token}")]
    [InlineData("/api/catalog/{type}/makes")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/demo/seed")]
    public async Task Document_contains_all_feature_routes(string path)
    {
        using var document = await LoadDocumentAsync(factory.CreateClient());

        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty(path, out _),
            $"O documento OpenAPI não traz a rota {path}.");
    }

    [Fact]
    public async Task Spa_fallback_does_not_appear_in_document()
    {
        var webRoot = NewTempDirectory();
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><div id=\"root\"></div>");
        using var spa = factory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot));
        using var client = spa.CreateClient();

        // O fallback está de fato mapeado neste host: sem isto o teste passaria à toa.
        using var deepLink = await client.GetAsync("/vehicles/abc");
        Assert.Equal(HttpStatusCode.OK, deepLink.StatusCode);
        Assert.Equal("text/html", deepLink.Content.Headers.ContentType?.MediaType);

        using var document = await LoadDocumentAsync(client);
        var foreign = document.RootElement.GetProperty("paths").EnumerateObject()
            .Select(path => path.Name)
            .Where(path => !path.StartsWith("/api/", StringComparison.Ordinal))
            .ToList();

        Assert.True(foreign.Count == 0, $"Rotas fora de /api no documento: {string.Join(", ", foreign)}.");
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

    private static async Task<JsonDocument> LoadDocumentAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Cada operação (método + rota) do documento; ignora o que não é método HTTP (<c>parameters</c>, <c>summary</c>).</summary>
    private static IEnumerable<(string Method, string Path, JsonElement Operation)> Operations(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(item => HttpMethods.Contains(item.Name))
                .Select(item => (item.Name.ToUpperInvariant(), path.Name, item.Value)));

    private static bool HasSchema(JsonElement mediaType) =>
        mediaType.TryGetProperty("schema", out var schema)
        && schema is { ValueKind: JsonValueKind.Object }
        && schema.EnumerateObject().Any();

    private static void AssertNoFailures(List<string> failures) =>
        Assert.True(failures.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, failures));

    private string NewTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "garage-tests", "openapi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }
}
