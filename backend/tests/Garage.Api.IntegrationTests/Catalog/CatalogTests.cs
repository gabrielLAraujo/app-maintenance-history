using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Api.IntegrationTests.Catalog;

/// <summary>
/// A tabela <c>catalog_cache</c> e a FIPE falsa são compartilhadas por todos os testes da fábrica: cada teste começa com o
/// cache vazio, a FIPE falsa sem respostas e o relógio fixo, e a devolve limpa ao terminar. Assim nenhum depende de linhas
/// gravadas por outro nem da ordem de execução.
/// </summary>
[Collection("api")]
public sealed class CatalogTests(GarageApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    private const string MakesUrl = "/api/catalog/car/makes";
    private const string ModelsUrl = "/api/catalog/car/makes/25/models";
    private const string YearsUrl = "/api/catalog/car/makes/25/models/1263/years";
    private const string DetailsUrl = "/api/catalog/car/makes/25/models/1263/years/1998-1";

    private const string FipeBrands = "cars/brands";
    private const string FipeModels = "cars/brands/25/models";
    private const string FipeYears = "cars/brands/25/models/1263/years";
    private const string FipeDetails = "cars/brands/25/models/1263/years/1998-1";
    private const string FipePrice = "cars/005340-6/years/1998-1";

    private const string UnavailableMessage = "Catálogo FIPE indisponível no momento. Preencha manualmente.";

    public async Task InitializeAsync()
    {
        factory.Fipe.Reset();
        factory.Clock.SetUtcNow(Now);
        await ClearCacheAsync();
    }

    public Task DisposeAsync()
    {
        factory.Fipe.Reset();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Makes_are_translated_and_cached()
    {
        factory.Fipe.Responses[FipeBrands] = """[{"code":"25","name":"Honda"}]""";
        using var client = await factory.CreateUserClientAsync();

        using var first = await client.GetAsync(MakesUrl);
        using var second = await client.GetAsync(MakesUrl);

        foreach (var response in new[] { first, second })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var make = Assert.Single((await Json(response)).EnumerateArray());
            Assert.Equal("25", make.GetProperty("id").GetString());
            Assert.Equal("Honda", make.GetProperty("name").GetString());
        }

        Assert.Equal(1, factory.Fipe.Calls);
        Assert.Equal(["fipe:cars/brands"], await CachedKeysAsync());
    }

    [Fact]
    public async Task Models_include_suggested_model_and_version()
    {
        factory.Fipe.Responses[FipeModels] = Fixture("models-honda.json");
        using var client = await factory.CreateUserClientAsync();

        using var response = await client.GetAsync(ModelsUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var models = (await Json(response)).EnumerateArray().ToList();
        Assert.Equal(3, models.Count);
        Assert.Equal("1263", models[0].GetProperty("id").GetString());
        Assert.Equal("Civic Sedan LX 1.6 16V Mec. 4p", models[0].GetProperty("fullName").GetString());
        Assert.Equal("Civic", models[0].GetProperty("suggestedModel").GetString());
        Assert.Equal("Sedan LX 1.6 16V Mec. 4p", models[0].GetProperty("suggestedVersion").GetString());
        Assert.Equal("Civic", models[2].GetProperty("suggestedModel").GetString());
        Assert.Equal("CRX/ Targa VTi", models[2].GetProperty("suggestedVersion").GetString());
    }

    [Fact]
    public async Task Years_parse_model_year_and_fuel()
    {
        factory.Fipe.Responses[FipeYears] = Fixture("years-sample.json");
        using var client = await factory.CreateUserClientAsync();

        using var response = await client.GetAsync(YearsUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var years = (await Json(response)).EnumerateArray().ToList();
        Assert.Equal(["1998-1", "2022-3", "32000-1"], years.Select(year => year.GetProperty("id").GetString()));
        Assert.Equal(1998, years[0].GetProperty("modelYear").GetInt32());
        Assert.Equal("gasoline", years[0].GetProperty("fuel").GetString());
        Assert.Equal("1998 Gasolina", years[0].GetProperty("label").GetString());
        Assert.Equal(2022, years[1].GetProperty("modelYear").GetInt32());
        Assert.Equal("diesel", years[1].GetProperty("fuel").GetString());
        // 32000 é o código da FIPE para zero km: não é um ano.
        Assert.Equal(JsonValueKind.Null, years[2].GetProperty("modelYear").ValueKind);
        Assert.Equal("gasoline", years[2].GetProperty("fuel").GetString());
    }

    [Fact]
    public async Task Details_translate_price_and_model_year()
    {
        factory.Fipe.Responses[FipeDetails] = Fixture("detail-sample.json");
        using var client = await factory.CreateUserClientAsync();

        using var response = await client.GetAsync(DetailsUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await Json(response);
        Assert.Equal("005340-6", details.GetProperty("fipeCode").GetString());
        Assert.Equal("Honda", details.GetProperty("make").GetString());
        Assert.Equal("Civic Sedan LX 1.6 16V Mec. 4p", details.GetProperty("fullName").GetString());
        Assert.Equal(1998, details.GetProperty("modelYear").GetInt32());
        Assert.Equal("gasoline", details.GetProperty("fuel").GetString());
        Assert.Equal(10000.00m, details.GetProperty("price").GetDecimal());
        Assert.Equal("outubro de 2026", details.GetProperty("referenceMonth").GetString());
    }

    [Fact]
    public async Task Fipe_down_without_cache_returns_503()
    {
        factory.Fipe.IsDown = true;
        using var client = await factory.CreateUserClientAsync();

        using var response = await client.GetAsync(MakesUrl);

        var problem = await response.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "catalog.unavailable");
        Assert.Equal(UnavailableMessage, problem.GetProperty("detail").GetString());
        Assert.True(factory.Fipe.Calls > 0, "A FIPE deveria ter sido consultada.");
        Assert.Empty(await CachedKeysAsync());
    }

    [Fact]
    public async Task Fipe_down_with_expired_cache_serves_stale()
    {
        factory.Fipe.Responses[FipeBrands] = """[{"code":"25","name":"Honda"}]""";
        using var client = await factory.CreateUserClientAsync();
        using var populate = await client.GetAsync(MakesUrl);
        Assert.Equal(HttpStatusCode.OK, populate.StatusCode);
        await AgeCacheAsync(FipeBrands, days: 31);
        factory.Fipe.IsDown = true;
        var callsBefore = factory.Fipe.Calls;

        using var response = await client.GetAsync(MakesUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Honda", Assert.Single((await Json(response)).EnumerateArray()).GetProperty("name").GetString());
        // Servido vencido de propósito: a FIPE foi tentada (e falhou) antes de o cache velho ser usado.
        Assert.True(factory.Fipe.Calls > callsBefore, "O cache vencido só deve servir depois de a FIPE falhar.");
        Assert.Equal(Now.AddDays(-31), await FetchedAtAsync(FipeBrands));
    }

    [Fact]
    public async Task Expired_cache_is_refreshed_when_fipe_answers()
    {
        factory.Fipe.Responses[FipeBrands] = """[{"code":"25","name":"Honda"}]""";
        using var client = await factory.CreateUserClientAsync();
        using var populate = await client.GetAsync(MakesUrl);
        await AgeCacheAsync(FipeBrands, days: 31);
        factory.Fipe.Responses[FipeBrands] = """[{"code":"25","name":"Honda Motors"}]""";

        using var refreshed = await client.GetAsync(MakesUrl);
        using var again = await client.GetAsync(MakesUrl);

        foreach (var response in new[] { refreshed, again })
            Assert.Equal("Honda Motors", Assert.Single((await Json(response)).EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(2, factory.Fipe.Calls); // a primeira carga e a renovação; a terceira leitura veio do cache
        Assert.Equal(["fipe:cars/brands"], await CachedKeysAsync()); // o upsert atualiza a linha, não duplica
        Assert.Equal(Now, await FetchedAtAsync(FipeBrands));
    }

    [Theory]
    [InlineData(MakesUrl, FipeBrands, 29, false)]
    [InlineData(MakesUrl, FipeBrands, 31, true)]
    [InlineData(ModelsUrl, FipeModels, 29, false)]
    [InlineData(ModelsUrl, FipeModels, 31, true)]
    [InlineData(YearsUrl, FipeYears, 29, false)]
    [InlineData(YearsUrl, FipeYears, 31, true)]
    [InlineData(DetailsUrl, FipeDetails, 6, false)]
    [InlineData(DetailsUrl, FipeDetails, 8, true)]
    public async Task Cache_is_valid_for_30_days_for_lists_and_7_days_for_details(
        string url, string fipePath, int ageInDays, bool expectRefetch)
    {
        ArrangeFipe();
        using var client = await factory.CreateUserClientAsync();
        using var populate = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, populate.StatusCode);
        Assert.Equal(1, factory.Fipe.Calls);
        await AgeCacheAsync(fipePath, ageInDays);

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectRefetch ? 2 : 1, factory.Fipe.Calls);
    }

    [Fact]
    public async Task Fipe_payload_that_cannot_be_translated_is_unavailable_and_never_cached()
    {
        factory.Fipe.Responses[FipeBrands] = "<html>502 Bad Gateway</html>";
        using var client = await factory.CreateUserClientAsync();

        using var response = await client.GetAsync(MakesUrl);

        await response.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "catalog.unavailable");
        Assert.Empty(await CachedKeysAsync());
    }

    [Fact]
    public async Task Catalog_requires_login()
    {
        ArrangeFipe();
        using var anonymous = factory.CreateClient();

        foreach (var url in new[] { MakesUrl, ModelsUrl, YearsUrl, DetailsUrl, $"/api/vehicles/{Guid.NewGuid()}/fipe-price" })
        {
            using var response = await anonymous.GetAsync(url);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        Assert.Equal(0, factory.Fipe.Calls);
    }

    [Fact]
    public async Task Invalid_vehicle_type_returns_400()
    {
        ArrangeFipe();
        using var client = await factory.CreateUserClientAsync();

        foreach (var url in new[] { "/api/catalog/boat/makes", "/api/catalog/1/makes/25/models", "/api/catalog/boat/makes/25/models/1263/years/1998-1" })
        {
            using var response = await client.GetAsync(url);

            var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
            Assert.Equal(["type"], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name));
        }

        Assert.Equal(0, factory.Fipe.Calls);
    }

    [Theory]
    [InlineData("/api/catalog/car/makes/a.b/models", "makeId")]
    [InlineData("/api/catalog/car/makes/25/models/a.b/years", "modelId")]
    [InlineData("/api/catalog/car/makes/25/models/1263/years/a.b", "yearId")]
    [InlineData("/api/catalog/car/makes/25/models/1263/years/1998-1%3Fx%3D1", "yearId")]
    public async Task Invalid_catalog_ids_return_400_without_reaching_fipe(string url, string field)
    {
        ArrangeFipe();
        using var client = await factory.CreateUserClientAsync();

        using var response = await client.GetAsync(url);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal([field], problem.GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.Equal(0, factory.Fipe.Calls);
    }

    [Fact]
    public async Task Vehicle_fipe_price_for_linked_vehicle()
    {
        ArrangeFipe();
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(catalog: TestData.Catalog("005340-6", "1998-1")));

        using var response = await client.GetAsync($"/api/vehicles/{vehicleId}/fipe-price");
        using var again = await client.GetAsync($"/api/vehicles/{vehicleId}/fipe-price");

        foreach (var reply in new[] { response, again })
        {
            Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
            var price = await Json(reply);
            Assert.Equal(10000.00m, price.GetProperty("price").GetDecimal());
            Assert.Equal("outubro de 2026", price.GetProperty("referenceMonth").GetString());
        }

        Assert.Equal(1, factory.Fipe.Calls);
    }

    [Fact]
    public async Task Vehicle_without_catalog_returns_404()
    {
        ArrangeFipe();
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        using var response = await client.GetAsync($"/api/vehicles/{vehicleId}/fipe-price");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.catalog_not_linked");
        Assert.Equal(0, factory.Fipe.Calls);
    }

    [Fact]
    public async Task Fipe_price_of_a_vehicle_the_user_cannot_see_returns_404()
    {
        ArrangeFipe();
        using var owner = await factory.CreateUserClientAsync();
        var vehicleId = await owner.CreateVehicleAsync(TestData.Vehicle(catalog: TestData.Catalog("005340-6", "1998-1")));
        using var stranger = await factory.CreateUserClientAsync();

        using var response = await stranger.GetAsync($"/api/vehicles/{vehicleId}/fipe-price");
        using var unknown = await stranger.GetAsync($"/api/vehicles/{Guid.NewGuid()}/fipe-price");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        Assert.Equal(0, factory.Fipe.Calls);
    }

    [Fact]
    public async Task Vehicle_fipe_price_with_fipe_down_returns_503()
    {
        factory.Fipe.IsDown = true;
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(catalog: TestData.Catalog("005340-6", "1998-1")));

        using var response = await client.GetAsync($"/api/vehicles/{vehicleId}/fipe-price");

        await response.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "catalog.unavailable");
    }

    [Fact]
    public async Task Catalog_is_rate_limited_per_user()
    {
        ArrangeFipe();
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:CatalogPerMinute", "2"));
        using var client = await limited.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(catalog: TestData.Catalog("005340-6", "1998-1")));

        using var first = await client.GetAsync(MakesUrl);
        // A cota é da política "catalog", compartilhada pelas rotas do catálogo e pelo preço do veículo.
        using var second = await client.GetAsync($"/api/vehicles/{vehicleId}/fipe-price");
        using var third = await client.GetAsync(MakesUrl);
        using var otherUser = await limited.CreateUserClientAsync();
        using var otherFirst = await otherUser.GetAsync(MakesUrl);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await third.AssertProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
        Assert.Equal(HttpStatusCode.OK, otherFirst.StatusCode);
    }

    /// <summary>Honda (25) → Civic Sedan LX (1263) → 1998 Gasolina, com o preço de R$ 10.000,00 do veículo de código 005340-6.</summary>
    private void ArrangeFipe()
    {
        factory.Fipe.Responses[FipeBrands] = """[{"code":"25","name":"Honda"}]""";
        factory.Fipe.Responses[FipeModels] = Fixture("models-honda.json");
        factory.Fipe.Responses[FipeYears] = Fixture("years-sample.json");
        factory.Fipe.Responses[FipeDetails] = Fixture("detail-sample.json");
        factory.Fipe.Responses[FipePrice] = Fixture("detail-sample.json");
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Catalog", "Fixtures", name));

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);

    private async Task ClearCacheAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GarageDbContext>().Database.ExecuteSqlRawAsync("DELETE FROM catalog_cache");
    }

    private async Task AgeCacheAsync(string fipePath, int days)
    {
        var key = $"fipe:{fipePath}";
        var fetchedAt = Now.AddDays(-days);
        await using var scope = factory.Services.CreateAsyncScope();
        var updated = await scope.ServiceProvider.GetRequiredService<GarageDbContext>().Database
            .ExecuteSqlInterpolatedAsync($"UPDATE catalog_cache SET fetched_at = {fetchedAt} WHERE key = {key}");
        Assert.Equal(1, updated);
    }

    private async Task<List<string>> CachedKeysAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GarageDbContext>().Database
            .SqlQuery<string>($"SELECT key AS \"Value\" FROM catalog_cache ORDER BY key")
            .ToListAsync();
    }

    private async Task<DateTimeOffset> FetchedAtAsync(string fipePath)
    {
        var key = $"fipe:{fipePath}";
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<GarageDbContext>().Database
            .SqlQuery<DateTimeOffset>($"SELECT fetched_at AS \"Value\" FROM catalog_cache WHERE key = {key}")
            .ToListAsync();
        return rows.Single();
    }
}
