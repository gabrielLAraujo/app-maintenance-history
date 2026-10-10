using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Garage.Api.IntegrationTests.Infrastructure;

public static class TestJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

public static class TestHttpExtensions
{
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage r, HttpStatusCode status, string code)
    {
        var body = await r.Content.ReadAsStringAsync();

        Assert.True(r.StatusCode == status,
            $"Expected {(int)status} {status} but got {(int)r.StatusCode} {r.StatusCode}: {body}");
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);

        var problem = JsonSerializer.Deserialize<JsonElement>(body, TestJson.Options);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _), $"ProblemDetails without traceId: {body}");
        return problem;
    }
}
