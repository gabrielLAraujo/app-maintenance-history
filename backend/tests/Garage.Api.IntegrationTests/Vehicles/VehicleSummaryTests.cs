using System.Net;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;

namespace Garage.Api.IntegrationTests.Vehicles;

[Collection("api")]
public sealed class VehicleSummaryTests(GarageApiFactory factory)
{
    // 15:00 UTC é 12:00 em São Paulo: "hoje" é 2026-10-10, e os 12 meses vão de 2025-11 a 2026-10.
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Summary_totals_types_categories_months_and_cost_per_km()
    {
        using var owner = await UserAsync();
        var vehicleId = await SeedCivicAsync(owner);

        var summary = await GetSummaryAsync(owner, vehicleId);

        Assert.Equal(6883.80m, summary.GetProperty("totalCost").GetDecimal());
        AssertType(summary.GetProperty("maintenance"), 2393.80m, 4);
        AssertType(summary.GetProperty("modification"), 4490m, 1);
        Assert.Equal("2024-11-11", summary.GetProperty("firstRecordDate").GetString());
        Assert.Equal(23_400, summary.GetProperty("kmTracked").GetInt32());
        // 6.883,80 ÷ 23.400 = 0,2941… → 0,29.
        Assert.Equal(0.29m, summary.GetProperty("costPerKm").GetDecimal());

        var categories = summary.GetProperty("byCategory").EnumerateArray()
            .Select(c => (c.GetProperty("category").GetString(), c.GetProperty("cost").GetDecimal()))
            .ToArray();
        Assert.Equal(
            [("suspension", 4490m), ("engine", 1275.80m), ("bodyPaint", 850m), ("brakes", 268m)],
            categories);

        var ordered = Months(summary);
        Assert.Equal(12, ordered.Length);
        Assert.Equal("2025-11", ordered[0].Month);
        Assert.Equal("2026-10", ordered[^1].Month);
        var months = ordered.ToDictionary(m => m.Month, m => m.Cost);
        Assert.Equal(4490m, months["2025-12"]);
        Assert.Equal(0m, months["2026-01"]);
        Assert.Equal(270.90m, months["2026-06"]);
        Assert.Equal(850m, months["2026-10"]);

        var last = summary.GetProperty("lastRecord");
        Assert.Equal("Repintura do para-choque dianteiro", last.GetProperty("title").GetString());
        Assert.Equal("2026-10-02", last.GetProperty("date").GetString());
        Assert.NotEqual(Guid.Empty, last.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Summary_of_a_vehicle_without_records_has_zeros_and_nulls()
    {
        using var owner = await UserAsync();
        var vehicleId = await owner.CreateVehicleAsync();

        var summary = await GetSummaryAsync(owner, vehicleId);

        Assert.Equal(0m, summary.GetProperty("totalCost").GetDecimal());
        AssertType(summary.GetProperty("maintenance"), 0m, 0);
        AssertType(summary.GetProperty("modification"), 0m, 0);
        foreach (var field in new[] { "firstRecordDate", "kmTracked", "costPerKm", "lastRecord" })
            Assert.Equal(JsonValueKind.Null, summary.GetProperty(field).ValueKind);
        Assert.Empty(summary.GetProperty("byCategory").EnumerateArray());
        var months = Months(summary);
        Assert.Equal(12, months.Length);
        Assert.All(months, month => Assert.Equal(0m, month.Cost));
    }

    [Fact]
    public async Task Records_without_km_nor_cost_count_but_add_no_cost()
    {
        using var owner = await UserAsync();
        var vehicleId = await owner.CreateVehicleAsync();
        await owner.CreateRecordAsync(vehicleId, TestData.Record(odometer: null));
        await owner.CreateRecordAsync(vehicleId, TestData.Record(title: "Lavagem", odometer: null, category: "bodyPaint"));

        var summary = await GetSummaryAsync(owner, vehicleId);

        Assert.Equal(0m, summary.GetProperty("totalCost").GetDecimal());
        AssertType(summary.GetProperty("maintenance"), 0m, 2);
        AssertType(summary.GetProperty("modification"), 0m, 0);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("kmTracked").ValueKind);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("costPerKm").ValueKind);
        Assert.Empty(summary.GetProperty("byCategory").EnumerateArray());
        Assert.All(Months(summary), month => Assert.Equal(0m, month.Cost));
    }

    [Fact]
    public async Task Cost_per_km_is_null_below_1000_km()
    {
        using var owner = await UserAsync();
        var vehicleId = await owner.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 152_300));
        await owner.CreateRecordAsync(vehicleId, TestData.Record(
            date: new DateOnly(2026, 9, 20), odometer: 151_500, laborCost: 300m));

        var summary = await GetSummaryAsync(owner, vehicleId);

        Assert.Equal(800, summary.GetProperty("kmTracked").GetInt32());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("costPerKm").ValueKind);
    }

    [Fact]
    public async Task Another_user_gets_404()
    {
        using var owner = await UserAsync();
        var vehicleId = await owner.CreateVehicleAsync();
        using var other = await factory.CreateUserClientAsync();

        using var response = await other.GetAsync($"/api/vehicles/{vehicleId}/summary");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
    }

    [Fact]
    public async Task Vehicle_list_brings_cover_total_and_last_record_date()
    {
        using var owner = await UserAsync();
        var vehicleId = await SeedCivicAsync(owner);
        var emptyId = await owner.CreateVehicleAsync();

        using var response = await owner.GetAsync("/api/vehicles");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"List vehicles failed: {(int)response.StatusCode} {content}");
        var items = JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options).EnumerateArray().ToList();

        var civic = items.Single(item => item.GetProperty("id").GetGuid() == vehicleId);
        Assert.Equal(6883.80m, civic.GetProperty("totalCost").GetDecimal());
        Assert.Equal("2026-10-02", civic.GetProperty("lastRecordDate").GetString());
        Assert.Equal(JsonValueKind.Null, civic.GetProperty("coverPhotoId").ValueKind);

        var empty = items.Single(item => item.GetProperty("id").GetGuid() == emptyId);
        Assert.Equal(0m, empty.GetProperty("totalCost").GetDecimal());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("lastRecordDate").ValueKind);
    }

    private async Task<HttpClient> UserAsync()
    {
        factory.Clock.SetUtcNow(Now);
        return await factory.CreateUserClientAsync();
    }

    /// <summary>
    /// Civic com 152.300 km e cinco registros: quatro manutenções (2.393,80) e uma modificação (4.490), de 11/11/2024
    /// (128.900 km) a 02/10/2026 (151.800 km).
    /// </summary>
    private static async Task<Guid> SeedCivicAsync(HttpClient owner)
    {
        var vehicleId = await owner.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 152_300));

        await owner.CreateRecordAsync(vehicleId, TestData.Record(
            title: "Revisão dos 130 mil", date: new DateOnly(2024, 11, 11), odometer: 128_900, category: "engine",
            laborCost: 450m,
            parts: [TestData.Part("Kit de correia dentada", unitPrice: 389.90m), TestData.Part("Bomba d'água", unitPrice: 165m)]));
        await owner.CreateRecordAsync(vehicleId, TestData.Record(
            title: "Pastilhas dianteiras", date: new DateOnly(2025, 3, 31), odometer: 133_600, category: "brakes",
            laborCost: 268m));
        await owner.CreateRecordAsync(vehicleId, TestData.Record(
            title: "Suspensão rebaixada", date: new DateOnly(2025, 12, 16), odometer: 142_200, type: "modification",
            category: "suspension", laborCost: 4490m));
        await owner.CreateRecordAsync(vehicleId, TestData.Record(
            title: "Troca de óleo", date: new DateOnly(2026, 6, 4), odometer: 147_800, category: "engine",
            laborCost: 270.90m));
        await owner.CreateRecordAsync(vehicleId, TestData.Record(
            title: "Repintura do para-choque dianteiro", date: new DateOnly(2026, 10, 2), odometer: 151_800,
            category: "bodyPaint", laborCost: 850m));

        return vehicleId;
    }

    private static async Task<JsonElement> GetSummaryAsync(HttpClient client, Guid vehicleId)
    {
        using var response = await client.GetAsync($"/api/vehicles/{vehicleId}/summary");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Get summary failed: {(int)response.StatusCode} {content}");

        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options);
    }

    private static void AssertType(JsonElement type, decimal cost, int count)
    {
        Assert.Equal(cost, type.GetProperty("cost").GetDecimal());
        Assert.Equal(count, type.GetProperty("count").GetInt32());
    }

    /// <summary>Os meses na ordem da resposta.</summary>
    private static (string Month, decimal Cost)[] Months(JsonElement summary) =>
        summary.GetProperty("byMonth").EnumerateArray()
            .Select(m => (m.GetProperty("month").GetString()!, m.GetProperty("cost").GetDecimal()))
            .ToArray();
}
