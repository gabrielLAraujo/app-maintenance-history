using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Garage.Api.IntegrationTests.Infrastructure;

/// <summary>Corpos de requisição e atalhos para preparar dados pela API. Os corpos são dicionários para os testes poderem alterar campos.</summary>
public static class TestData
{
    /// <summary>
    /// Corpo de <c>POST /api/vehicles</c>: Honda Civic EK 1997/1998, sem apelido. Para <c>PUT</c>, acrescente <c>rowVersion</c>
    /// com <see cref="With"/> (o <c>currentOdometer</c> extra é ignorado).
    /// </summary>
    public static Dictionary<string, object?> Vehicle(int currentOdometer = 150_000, object? catalog = null) => new()
    {
        ["type"] = "car",
        ["make"] = "Honda",
        ["model"] = "Civic",
        ["version"] = "Sedan LX 1.6 16V Mec. 4p",
        ["generation"] = "EK",
        ["manufactureYear"] = 1997,
        ["modelYear"] = 1998,
        ["fuel"] = "gasoline",
        ["nickname"] = null,
        ["plate"] = "abc-1234",
        ["vin"] = "1HGEJ6678WL000001",
        ["notes"] = "segredo-notas",
        ["currentOdometer"] = currentOdometer,
        ["catalog"] = catalog
    };

    public static object Catalog(string fipeCode = "014079-5", string fipeYearCode = "1998-1") =>
        new { type = "car", fipeCode, fipeYearCode };

    public static Dictionary<string, object?> With(this Dictionary<string, object?> body, string key, object? value)
    {
        body[key] = value;
        return body;
    }

    /// <summary>Cria um veículo (corpo padrão de <see cref="Vehicle"/>) e devolve o id.</summary>
    public static async Task<Guid> CreateVehicleAsync(this HttpClient client, Dictionary<string, object?>? body = null)
    {
        using var response = await client.PostAsJsonAsync("/api/vehicles", body ?? Vehicle(), TestJson.Options);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Create vehicle failed: {(int)response.StatusCode} {content}");

        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options).GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> GetVehicleAsync(this HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/vehicles/{id}");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Get vehicle failed: {(int)response.StatusCode} {content}");

        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options);
    }
}
