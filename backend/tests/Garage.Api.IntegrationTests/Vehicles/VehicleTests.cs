using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Vehicles;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Garage.Api.IntegrationTests.Vehicles;

[Collection("api")]
public sealed class VehicleTests(GarageApiFactory factory)
{
    [Fact]
    public async Task Create_and_get_roundtrip()
    {
        using var client = await factory.CreateUserClientAsync();

        using var created = await client.PostAsJsonAsync("/api/vehicles", TestData.Vehicle(), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
        Assert.Equal($"/api/vehicles/{id}", created.Headers.Location?.OriginalString);

        var vehicle = await client.GetVehicleAsync(id);
        Assert.Equal(id, vehicle.GetProperty("id").GetGuid());
        Assert.Equal("car", vehicle.GetProperty("type").GetString());
        Assert.Equal("Honda", vehicle.GetProperty("make").GetString());
        Assert.Equal("Civic", vehicle.GetProperty("model").GetString());
        Assert.Equal("Sedan LX 1.6 16V Mec. 4p", vehicle.GetProperty("version").GetString());
        Assert.Equal("EK", vehicle.GetProperty("generation").GetString());
        Assert.Equal(1997, vehicle.GetProperty("manufactureYear").GetInt32());
        Assert.Equal(1998, vehicle.GetProperty("modelYear").GetInt32());
        Assert.Equal("gasoline", vehicle.GetProperty("fuel").GetString());
        Assert.Equal(JsonValueKind.Null, vehicle.GetProperty("nickname").ValueKind);
        Assert.Equal("ABC1234", vehicle.GetProperty("plate").GetString());
        Assert.Equal("1HGEJ6678WL000001", vehicle.GetProperty("vin").GetString());
        Assert.Equal("segredo-notas", vehicle.GetProperty("notes").GetString());
        Assert.Equal(150_000, vehicle.GetProperty("currentOdometer").GetInt32());
        Assert.Equal(factory.Clock.GetUtcNow(), vehicle.GetProperty("odometerUpdatedAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, vehicle.GetProperty("catalog").ValueKind);
        Assert.True(vehicle.GetProperty("rowVersion").GetUInt32() > 0);
    }

    [Fact]
    public async Task Create_with_catalog_roundtrips_the_catalog_reference()
    {
        using var client = await factory.CreateUserClientAsync();
        var id = await client.CreateVehicleAsync(TestData.Vehicle(catalog: TestData.Catalog("014079-5", "1998-1")));

        var catalog = (await client.GetVehicleAsync(id)).GetProperty("catalog");

        Assert.Equal("car", catalog.GetProperty("type").GetString());
        Assert.Equal("014079-5", catalog.GetProperty("fipeCode").GetString());
        Assert.Equal("1998-1", catalog.GetProperty("fipeYearCode").GetString());
    }

    [Fact]
    public async Task Create_trims_texts_and_treats_blank_optionals_as_absent()
    {
        using var client = await factory.CreateUserClientAsync();
        var body = TestData.Vehicle()
            .With("make", "  Honda ")
            .With("version", "")
            .With("generation", " ")
            .With("nickname", "   ")
            .With("plate", "")
            .With("vin", "")
            .With("notes", " ");
        var id = await client.CreateVehicleAsync(body);

        var vehicle = await client.GetVehicleAsync(id);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/vehicles", TestJson.Options);

        Assert.Equal("Honda", vehicle.GetProperty("make").GetString());
        foreach (var field in new[] { "version", "generation", "nickname", "plate", "vin", "notes" })
            Assert.Equal(JsonValueKind.Null, vehicle.GetProperty(field).ValueKind);
        // Um apelido em branco não pode apagar o nome de exibição.
        Assert.Equal("Honda Civic 1998", list[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Create_with_invalid_fields_returns_400_with_field_errors()
    {
        using var client = await factory.CreateUserClientAsync();
        var body = TestData.Vehicle().With("make", "").With("plate", "XX").With("manufactureYear", 1997).With("modelYear", 2000);

        using var response = await client.PostAsJsonAsync("/api/vehicles", body, TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "make", "modelYear", "plate" }, FieldNames(errors));
        Assert.Equal("Informe a marca.", errors.GetProperty("make")[0].GetString());
        Assert.Equal("Placa inválida.", errors.GetProperty("plate")[0].GetString());
        Assert.Equal("Ano inválido.", errors.GetProperty("modelYear")[0].GetString());
    }

    [Fact]
    public async Task Create_with_too_long_fields_returns_400_with_field_errors()
    {
        using var client = await factory.CreateUserClientAsync();
        var body = TestData.Vehicle()
            .With("make", new string('a', 61))
            .With("model", new string('a', 61))
            .With("version", new string('a', 121))
            .With("generation", new string('a', 31))
            .With("nickname", new string('a', 61))
            .With("vin", new string('a', 18))
            .With("notes", new string('a', 2001));

        using var response = await client.PostAsJsonAsync("/api/vehicles", body, TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "generation", "make", "model", "nickname", "notes", "version", "vin" }, FieldNames(errors));
    }

    [Fact]
    public async Task Create_with_invalid_odometer_enums_and_catalog_returns_400_with_field_errors()
    {
        using var client = await factory.CreateUserClientAsync();
        var body = TestData.Vehicle(currentOdometer: 2_000_001, catalog: new { type = 99, fipeCode = "", fipeYearCode = new string('1', 21) })
            .With("type", 99)
            .With("fuel", 99);

        using var response = await client.PostAsJsonAsync("/api/vehicles", body, TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(
            new[] { "catalog.fipeCode", "catalog.fipeYearCode", "catalog.type", "currentOdometer", "fuel", "type" },
            FieldNames(errors));
    }

    [Fact]
    public async Task List_returns_only_my_vehicles()
    {
        using var mine = await factory.CreateUserClientAsync();
        using var theirs = await factory.CreateUserClientAsync();
        var first = await mine.CreateVehicleAsync();
        var second = await mine.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 90_000).With("nickname", "Meu EK").With("plate", null));
        var other = await theirs.CreateVehicleAsync();

        var myList = await mine.GetFromJsonAsync<JsonElement>("/api/vehicles", TestJson.Options);
        var theirList = await theirs.GetFromJsonAsync<JsonElement>("/api/vehicles", TestJson.Options);

        var items = myList.EnumerateArray().ToList();
        Assert.Equal(new[] { first, second }, items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal("Honda Civic 1998", items[0].GetProperty("displayName").GetString());
        Assert.Equal("Honda", items[0].GetProperty("make").GetString());
        Assert.Equal("Civic", items[0].GetProperty("model").GetString());
        Assert.Equal(1998, items[0].GetProperty("modelYear").GetInt32());
        Assert.Equal("ABC1234", items[0].GetProperty("plate").GetString());
        Assert.Equal(150_000, items[0].GetProperty("currentOdometer").GetInt32());
        Assert.Equal("Meu EK", items[1].GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("plate").ValueKind);
        Assert.Equal(90_000, items[1].GetProperty("currentOdometer").GetInt32());
        Assert.Equal(new[] { other }, theirList.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Other_user_gets_404_on_get_update_delete_and_odometer()
    {
        using var owner = await factory.CreateUserClientAsync();
        using var other = await factory.CreateUserClientAsync();
        var id = await owner.CreateVehicleAsync();
        var rowVersion = (await owner.GetVehicleAsync(id)).GetProperty("rowVersion").GetUInt32();
        var url = $"/api/vehicles/{id}";

        using var get = await other.GetAsync(url);
        using var put = await other.PutAsJsonAsync(url, TestData.Vehicle().With("nickname", "Roubado").With("rowVersion", rowVersion), TestJson.Options);
        using var odometer = await other.PostAsJsonAsync($"{url}/odometer", new { km = 200_000 }, TestJson.Options);
        using var delete = await other.DeleteAsync(url);

        await get.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await put.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await odometer.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await delete.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        var untouched = await owner.GetVehicleAsync(id);
        Assert.Equal(JsonValueKind.Null, untouched.GetProperty("nickname").ValueKind);
        Assert.Equal(150_000, untouched.GetProperty("currentOdometer").GetInt32());
        Assert.Equal(rowVersion, untouched.GetProperty("rowVersion").GetUInt32());
    }

    [Fact]
    public async Task Unknown_vehicle_gets_404()
    {
        using var client = await factory.CreateUserClientAsync();

        using var response = await client.GetAsync($"/api/vehicles/{Guid.NewGuid()}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
    }

    [Fact]
    public async Task Update_changes_fields_and_replaces_or_clears_the_catalog()
    {
        using var client = await factory.CreateUserClientAsync();
        var id = await client.CreateVehicleAsync(TestData.Vehicle(catalog: TestData.Catalog("014079-5", "1998-1")));
        var url = $"/api/vehicles/{id}";
        var before = (await client.GetVehicleAsync(id)).GetProperty("rowVersion").GetUInt32();

        var replace = TestData.Vehicle(catalog: TestData.Catalog("999999-9", "2001-1"))
            .With("nickname", "Meu EK").With("plate", "BRA2E19").With("modelYear", 1997).With("rowVersion", before);
        using var replaced = await client.PutAsJsonAsync(url, replace, TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);
        var vehicle = await client.GetVehicleAsync(id);
        Assert.Equal("Meu EK", vehicle.GetProperty("nickname").GetString());
        Assert.Equal("BRA2E19", vehicle.GetProperty("plate").GetString());
        Assert.Equal(1997, vehicle.GetProperty("modelYear").GetInt32());
        Assert.Equal("999999-9", vehicle.GetProperty("catalog").GetProperty("fipeCode").GetString());
        Assert.Equal("2001-1", vehicle.GetProperty("catalog").GetProperty("fipeYearCode").GetString());
        var after = vehicle.GetProperty("rowVersion").GetUInt32();
        Assert.NotEqual(before, after);

        using var cleared = await client.PutAsJsonAsync(url, TestData.Vehicle().With("plate", null).With("rowVersion", after), TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        var withoutCatalog = await client.GetVehicleAsync(id);
        Assert.Equal(JsonValueKind.Null, withoutCatalog.GetProperty("catalog").ValueKind);
        Assert.Equal(JsonValueKind.Null, withoutCatalog.GetProperty("plate").ValueKind);
    }

    [Fact]
    public async Task Update_with_invalid_fields_returns_400_with_field_errors()
    {
        using var client = await factory.CreateUserClientAsync();
        var id = await client.CreateVehicleAsync();
        var rowVersion = (await client.GetVehicleAsync(id)).GetProperty("rowVersion").GetUInt32();

        using var response = await client.PutAsJsonAsync(
            $"/api/vehicles/{id}", TestData.Vehicle().With("make", "").With("rowVersion", rowVersion), TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "make" }, FieldNames(errors));
    }

    [Fact]
    public async Task Update_with_stale_row_version_returns_409()
    {
        using var client = await factory.CreateUserClientAsync();
        var id = await client.CreateVehicleAsync();
        var url = $"/api/vehicles/{id}";
        var stale = (await client.GetVehicleAsync(id)).GetProperty("rowVersion").GetUInt32();

        // O primeiro PUT muda um campo de verdade; sem alteração o EF nem emite UPDATE e a versão não avança.
        using var first = await client.PutAsJsonAsync(url, TestData.Vehicle().With("nickname", "Primeiro").With("rowVersion", stale), TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        var current = (await client.GetVehicleAsync(id)).GetProperty("rowVersion").GetUInt32();
        Assert.NotEqual(stale, current);

        using var second = await client.PutAsJsonAsync(url, TestData.Vehicle().With("nickname", "Segundo").With("rowVersion", stale), TestJson.Options);

        await second.AssertProblemAsync(HttpStatusCode.Conflict, "concurrency.conflict");
        Assert.Equal("Primeiro", (await client.GetVehicleAsync(id)).GetProperty("nickname").GetString());

        // Com a versão atual o mesmo PUT passa.
        using var retry = await client.PutAsJsonAsync(url, TestData.Vehicle().With("nickname", "Segundo").With("rowVersion", current), TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.Equal("Segundo", (await client.GetVehicleAsync(id)).GetProperty("nickname").GetString());
    }

    [Fact]
    public async Task UpdateOdometer_higher_or_equal_updates_current_odometer_and_timestamp()
    {
        // Relógio próprio: o da fábrica é compartilhado com os outros testes e não volta no tempo.
        var clock = new FakeTimeProvider(factory.Clock.GetUtcNow());
        using var timed = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = await timed.CreateUserClientAsync();
        var id = await client.CreateVehicleAsync();
        var url = $"/api/vehicles/{id}/odometer";
        var created = clock.GetUtcNow();
        clock.Advance(TimeSpan.FromHours(1));

        using var same = await client.PostAsJsonAsync(url, new { km = 150_000 }, TestJson.Options);
        using var higher = await client.PostAsJsonAsync(url, new { km = 151_234 }, TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, same.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, higher.StatusCode);
        var vehicle = await client.GetVehicleAsync(id);
        Assert.Equal(151_234, vehicle.GetProperty("currentOdometer").GetInt32());
        Assert.Equal(created.AddHours(1), vehicle.GetProperty("odometerUpdatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task UpdateOdometer_lower_returns_422()
    {
        using var client = await factory.CreateUserClientAsync();
        var id = await client.CreateVehicleAsync();

        using var response = await client.PostAsJsonAsync($"/api/vehicles/{id}/odometer", new { km = 149_999 }, TestJson.Options);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "vehicle.odometer_cannot_decrease");
        Assert.Equal(150_000, (await client.GetVehicleAsync(id)).GetProperty("currentOdometer").GetInt32());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2_000_001)]
    public async Task UpdateOdometer_out_of_range_returns_400(int km)
    {
        using var client = await factory.CreateUserClientAsync();
        var id = await client.CreateVehicleAsync();

        using var response = await client.PostAsJsonAsync($"/api/vehicles/{id}/odometer", new { km }, TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "km" }, FieldNames(errors));
    }

    [Fact]
    public async Task Delete_then_get_returns_404()
    {
        using var client = await factory.CreateUserClientAsync();
        var id = await client.CreateVehicleAsync();

        using var deleted = await client.DeleteAsync($"/api/vehicles/{id}");
        using var get = await client.GetAsync($"/api/vehicles/{id}");
        using var deletedAgain = await client.DeleteAsync($"/api/vehicles/{id}");
        var list = await client.GetFromJsonAsync<JsonElement>("/api/vehicles", TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await get.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await deletedAgain.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task Deleting_the_owner_deletes_their_vehicles()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        using var client = await factory.CreateUserClientAsync(email);
        var id = await client.CreateVehicleAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.True((await users.DeleteAsync(user!)).Succeeded);
        }

        await using var readScope = factory.Services.CreateAsyncScope();
        var stillThere = await readScope.ServiceProvider.GetRequiredService<IReadDbContext>()
            .Query<Vehicle>()
            .AnyAsync(vehicle => vehicle.Id == id);
        Assert.False(stillThere);
    }

    private static string[] FieldNames(JsonElement errors) =>
        errors.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
}
