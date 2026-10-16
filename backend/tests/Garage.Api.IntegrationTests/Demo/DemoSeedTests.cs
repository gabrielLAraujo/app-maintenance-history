using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Garage.Api.IntegrationTests.Demo;

[Collection("api")]
public sealed class DemoSeedTests(GarageApiFactory factory)
{
    private const string SeedUrl = "/api/demo/seed";

    // 15:00 UTC é 12:00 em São Paulo: "hoje" é 2026-10-10.
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 10);

    [Fact]
    public async Task Demo_seed_creates_two_vehicles_with_records_and_reminders()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();

        var seeded = await SeedAsync(client);

        Assert.Equal(2, seeded.GetProperty("vehicles").GetInt32());
        Assert.Equal(8, seeded.GetProperty("records").GetInt32());
        Assert.Equal(4, seeded.GetProperty("reminders").GetInt32());
        Assert.Equal(2, (await GetVehiclesAsync(client)).Count);
        await AssertOnlyCivicRemindersAreDueAsync(client);
    }

    [Fact]
    public async Task Demo_seed_twice_returns_409()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);

        using var again = await client.PostAsync(SeedUrl, content: null);

        await again.AssertProblemAsync(HttpStatusCode.Conflict, "demo.already_has_data");
        Assert.Equal(2, (await GetVehiclesAsync(client)).Count);
    }

    [Fact]
    public async Task Demo_seed_is_rejected_for_a_user_who_already_has_a_vehicle()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await client.CreateVehicleAsync();

        using var response = await client.PostAsync(SeedUrl, content: null);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "demo.already_has_data");
        Assert.Single(await GetVehiclesAsync(client));
    }

    [Fact]
    public async Task Demo_seed_only_looks_at_the_callers_own_vehicles()
    {
        factory.Clock.SetUtcNow(Now);
        using var other = await factory.CreateUserClientAsync();
        await other.CreateVehicleAsync();
        using var client = await factory.CreateUserClientAsync();

        await SeedAsync(client);

        Assert.Single(await GetVehiclesAsync(other));
        Assert.Equal(2, (await GetVehiclesAsync(client)).Count);
    }

    [Fact]
    public async Task Demo_seed_is_404_when_disabled()
    {
        using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Demo:Enabled", "false"));
        using var client = await disabled.CreateUserClientAsync();

        using var response = await client.PostAsync(SeedUrl, content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await GetVehiclesAsync(client));
    }

    [Fact]
    public async Task Demo_seed_requires_login_and_antiforgery()
    {
        using var anonymous = await factory.CreateAnonymousClientAsync();
        using var withoutLogin = await anonymous.PostAsync(SeedUrl, content: null);

        using var user = await factory.CreateUserClientAsync();
        user.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        using var withoutToken = await user.PostAsync(SeedUrl, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutLogin.StatusCode);
        await withoutToken.AssertProblemAsync(HttpStatusCode.BadRequest, "auth.antiforgery_invalid");
        Assert.Empty(await GetVehiclesAsync(user));
    }

    [Fact]
    public async Task Demo_seed_builds_the_civic_history_through_the_domain_rules()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);
        var civicId = await FindVehicleIdAsync(client, "Civic EK");

        var civic = await client.GetVehicleAsync(civicId);
        Assert.Equal("Honda", civic.GetProperty("make").GetString());
        Assert.Equal("Civic", civic.GetProperty("model").GetString());
        Assert.Equal("Sedan LX 1.6 16V Mec. 4p", civic.GetProperty("version").GetString());
        Assert.Equal("EK", civic.GetProperty("generation").GetString());
        Assert.Equal(1997, civic.GetProperty("manufactureYear").GetInt32());
        Assert.Equal(1998, civic.GetProperty("modelYear").GetInt32());
        Assert.Equal("gasoline", civic.GetProperty("fuel").GetString());
        Assert.Equal("ABC1D23", civic.GetProperty("plate").GetString());
        Assert.Equal(152_300, civic.GetProperty("currentOdometer").GetInt32());

        // Mais recentes primeiro: datas entre hoje-700 e hoje-10; a km cresce com o tempo e nunca passa da atual.
        var records = (await client.GetRecordsAsync(civicId)).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(6, records.Count);
        var dates = records.Select(record => DateOnly.Parse(record.GetProperty("date").GetString()!)).ToList();
        Assert.Equal(Today.AddDays(-10), dates.First());
        Assert.Equal(Today.AddDays(-700), dates.Last());
        Assert.Equal(dates.OrderByDescending(date => date), dates);
        var kms = records.Select(record => record.GetProperty("odometer").GetInt32()).ToList();
        Assert.Equal(kms.OrderByDescending(km => km), kms);
        Assert.Equal(kms.Count, kms.Distinct().Count());
        Assert.All(kms, km => Assert.True(km <= 152_300, $"Registro com {km} km, acima da km atual."));

        var coilover = Assert.Single(records, record => record.GetProperty("type").GetString() == "modification");
        Assert.Equal("suspension", coilover.GetProperty("category").GetString());
        var coiloverDetail = await client.GetRecordAsync(civicId, coilover.GetProperty("id").GetGuid());
        Assert.Equal("Amortecedores originais guardados", coiloverDetail.GetProperty("replacedOriginalPart").GetString());

        var oil = Assert.Single(records, record => record.GetProperty("title").GetString()!.StartsWith("Troca de óleo", StringComparison.Ordinal));
        Assert.Equal("engine", oil.GetProperty("category").GetString());
        Assert.True(oil.GetProperty("totalCost").GetDecimal() > 0);
        var oilDetail = await client.GetRecordAsync(civicId, oil.GetProperty("id").GetGuid());
        var parts = oilDetail.GetProperty("parts").EnumerateArray().ToList();
        Assert.Equal(2, parts.Count);
        Assert.All(parts, part => Assert.True(part.GetProperty("unitPrice").GetDecimal() > 0));

        var states = (await client.GetRemindersAsync(civicId)).EnumerateArray()
            .Select(reminder => reminder.GetProperty("status").GetProperty("state").GetString())
            .Order()
            .ToList();
        Assert.Equal(new[] { "dueSoon", "ok", "overdue" }, states);
    }

    [Fact]
    public async Task Demo_seed_gives_the_civic_a_public_link_without_costs_or_plate()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);
        var civicId = await FindVehicleIdAsync(client, "Civic EK");
        var golId = await FindVehicleIdAsync(client, "Volkswagen Gol 2019");

        var link = Assert.Single((await client.GetShareLinksAsync(civicId)).EnumerateArray());
        Assert.True(link.GetProperty("isActive").GetBoolean());
        Assert.False(link.GetProperty("includeCosts").GetBoolean());
        Assert.False(link.GetProperty("includePlate").GetBoolean());
        Assert.Empty((await client.GetShareLinksAsync(golId)).EnumerateArray());

        using var anonymous = factory.CreateCookieClient();
        using var response = await anonymous.GetAsync($"/api/public/{link.GetProperty("token").GetString()}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Public view failed: {(int)response.StatusCode} {body}");
        var view = JsonSerializer.Deserialize<JsonElement>(body, TestJson.Options);
        Assert.Equal(6, view.GetProperty("records").GetArrayLength());
        Assert.False(view.GetProperty("includesCosts").GetBoolean());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("plate").ValueKind);
    }

    [Fact]
    public async Task Demo_seed_builds_the_gol_with_two_records_and_an_up_to_date_reminder()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);
        var golId = await FindVehicleIdAsync(client, "Volkswagen Gol 2019");

        var gol = await client.GetVehicleAsync(golId);
        Assert.Equal("1.0 MPI Flex 12V 5p", gol.GetProperty("version").GetString());
        Assert.Equal(2018, gol.GetProperty("manufactureYear").GetInt32());
        Assert.Equal(2019, gol.GetProperty("modelYear").GetInt32());
        Assert.Equal("flex", gol.GetProperty("fuel").GetString());
        Assert.Equal(48_000, gol.GetProperty("currentOdometer").GetInt32());
        Assert.Equal(2, (await client.GetRecordsAsync(golId)).GetProperty("items").GetArrayLength());
        var reminder = Assert.Single((await client.GetRemindersAsync(golId)).EnumerateArray());
        Assert.Equal("ok", reminder.GetProperty("status").GetProperty("state").GetString());
    }

    [Fact]
    public async Task Demo_seed_dates_are_relative_to_the_clock()
    {
        // 29/02/2028 é bissexto: confere que as datas relativas e os prazos em meses seguem o "hoje" da aplicação.
        var clock = new FakeTimeProvider(new DateTimeOffset(2028, 2, 29, 15, 0, 0, TimeSpan.Zero));
        using var timed = factory.WithClock(clock);
        using var client = await timed.CreateUserClientAsync();

        var seeded = await SeedAsync(client);

        Assert.Equal(8, seeded.GetProperty("records").GetInt32());
        var civicId = await FindVehicleIdAsync(client, "Civic EK");
        var newest = (await client.GetRecordsAsync(civicId)).GetProperty("items")[0];
        Assert.Equal("2028-02-19", newest.GetProperty("date").GetString());
        await AssertOnlyCivicRemindersAreDueAsync(client);
    }

    [Fact]
    public async Task Demo_seed_sets_the_civic_cover_photo()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);
        var civicId = await FindVehicleIdAsync(client, "Civic EK");
        var golId = await FindVehicleIdAsync(client, "Volkswagen Gol 2019");

        var civic = await client.GetVehicleAsync(civicId);
        var coverId = civic.GetProperty("coverPhotoId").GetGuid();

        // A capa é a foto da frente do Civic, anexada à repintura do para-choque.
        var records = (await client.GetRecordsAsync(civicId)).GetProperty("items").EnumerateArray();
        var bodywork = records.Single(record => record.GetProperty("title").GetString() == "Repintura do para-choque dianteiro");
        var detail = await client.GetRecordAsync(civicId, bodywork.GetProperty("id").GetGuid());
        var cover = Assert.Single(detail.GetProperty("attachments").EnumerateArray());
        Assert.Equal(coverId, cover.GetProperty("id").GetGuid());
        Assert.Equal("photo", cover.GetProperty("kind").GetString());
        Assert.Equal("para-choque.jpg", cover.GetProperty("fileName").GetString());
        Assert.Equal(JsonValueKind.Null, (await client.GetVehicleAsync(golId)).GetProperty("coverPhotoId").ValueKind);
    }

    private static async Task<JsonElement> SeedAsync(HttpClient client)
    {
        using var response = await client.PostAsync(SeedUrl, content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Seed failed: {(int)response.StatusCode} {body}");

        return JsonSerializer.Deserialize<JsonElement>(body, TestJson.Options);
    }

    private static async Task<List<JsonElement>> GetVehiclesAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/vehicles");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"List vehicles failed: {(int)response.StatusCode} {content}");

        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options).EnumerateArray().ToList();
    }

    private static async Task<Guid> FindVehicleIdAsync(HttpClient client, string displayName) =>
        Assert.Single(await GetVehiclesAsync(client), vehicle => vehicle.GetProperty("displayName").GetString() == displayName)
            .GetProperty("id").GetGuid();

    /// <summary>O painel mostra exatamente dois lembretes, ambos do Civic: o fluido de freio vencido e o óleo próximo.</summary>
    private static async Task AssertOnlyCivicRemindersAreDueAsync(HttpClient client)
    {
        var civicId = await FindVehicleIdAsync(client, "Civic EK");
        var due = (await client.GetFromJsonAsync<JsonElement>("/api/reminders/due", TestJson.Options)).EnumerateArray().ToList();

        Assert.Equal(2, due.Count);
        Assert.All(due, item => Assert.Equal(civicId, item.GetProperty("vehicleId").GetGuid()));
        Assert.Equal("overdue", due[0].GetProperty("status").GetProperty("state").GetString());
        Assert.Equal("Fluido de freio", due[0].GetProperty("title").GetString());
        Assert.Equal("dueSoon", due[1].GetProperty("status").GetProperty("state").GetString());
        Assert.StartsWith("Troca de óleo", due[1].GetProperty("title").GetString(), StringComparison.Ordinal);
    }
}
