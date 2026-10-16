using System.Net;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;

namespace Garage.Api.IntegrationTests.Garage;

[Collection("api")]
public sealed class GarageSummaryTests(GarageApiFactory factory)
{
    private const string SummaryUrl = "/api/garage/summary";

    // 15:00 UTC é 12:00 em São Paulo: "hoje" é 2026-10-12. Os dados de exemplo são relativos a hoje.
    private static readonly DateTimeOffset SeedNow = new(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Summary_of_the_year_across_vehicles()
    {
        factory.Clock.SetUtcNow(SeedNow);
        using var client = await factory.CreateUserClientAsync();
        await SeedDemoAsync(client);

        var summary = await GetSummaryAsync(client);

        // Em 2026: Civic, óleo (270,90) e repintura (850,00); Gol, óleo (206,10) e pneus (1.259,60).
        Assert.Equal(2026, summary.GetProperty("year").GetInt32());
        Assert.Equal(2586.60m, summary.GetProperty("yearCost").GetDecimal());
        Assert.Equal(4, summary.GetProperty("yearRecordCount").GetInt32());
        Assert.Equal(2, summary.GetProperty("yearVehicleCount").GetInt32());

        var months = Months(summary);
        Assert.Equal(12, months.Length);
        Assert.Equal("2026-01", months[0].Month);
        Assert.Equal("2026-12", months[^1].Month);
        Assert.Equal(1259.60m, months.Single(m => m.Month == "2026-07").Cost);
        Assert.Equal(0m, months.Single(m => m.Month == "2026-11").Cost);

        var recent = summary.GetProperty("recentRecords").EnumerateArray().ToList();
        Assert.Equal(4, recent.Count);
        var first = recent[0];
        Assert.Equal("Repintura do para-choque dianteiro", first.GetProperty("title").GetString());
        Assert.Equal("Civic EK", first.GetProperty("vehicleName").GetString());
        Assert.Equal("2026-10-02", first.GetProperty("date").GetString());
        Assert.Equal(850m, first.GetProperty("totalCost").GetDecimal());
        Assert.NotEqual(Guid.Empty, first.GetProperty("id").GetGuid());
        Assert.NotEqual(Guid.Empty, first.GetProperty("vehicleId").GetGuid());
        // Mais recentes primeiro.
        var dates = recent.Select(r => r.GetProperty("date").GetString()).ToList();
        Assert.Equal(dates.OrderDescending(StringComparer.Ordinal), dates);

        Assert.Equal(8, summary.GetProperty("recordCount").GetInt32());
        Assert.Equal("2024-11-11", summary.GetProperty("firstRecordDate").GetString());
    }

    [Fact]
    public async Task Year_follows_the_Sao_Paulo_time_zone()
    {
        // 02:00 UTC de 01/01/2027 ainda é 31/12/2026, 23:00, em São Paulo.
        factory.Clock.SetUtcNow(new DateTimeOffset(2027, 1, 1, 2, 0, 0, TimeSpan.Zero));
        using var client = await factory.CreateUserClientAsync();

        var summary = await GetSummaryAsync(client);

        Assert.Equal(2026, summary.GetProperty("year").GetInt32());
        var months = Months(summary);
        Assert.Equal("2026-01", months[0].Month);
        Assert.Equal("2026-12", months[^1].Month);
    }

    [Fact]
    public async Task Only_the_callers_vehicles_count()
    {
        factory.Clock.SetUtcNow(SeedNow);
        using var owner = await factory.CreateUserClientAsync();
        await SeedDemoAsync(owner);
        using var other = await factory.CreateUserClientAsync();

        var summary = await GetSummaryAsync(other);

        Assert.Equal(0, summary.GetProperty("recordCount").GetInt32());
        Assert.Equal(0m, summary.GetProperty("yearCost").GetDecimal());
        Assert.Equal(0, summary.GetProperty("yearRecordCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("yearVehicleCount").GetInt32());
        Assert.Empty(summary.GetProperty("recentRecords").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("firstRecordDate").ValueKind);
        Assert.All(Months(summary), month => Assert.Equal(0m, month.Cost));
    }

    private static async Task SeedDemoAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/api/demo/seed", content: null);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Demo seed failed: {(int)response.StatusCode} {content}");
    }

    private static async Task<JsonElement> GetSummaryAsync(HttpClient client)
    {
        using var response = await client.GetAsync(SummaryUrl);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Garage summary failed: {(int)response.StatusCode} {content}");

        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options);
    }

    private static (string Month, decimal Cost)[] Months(JsonElement summary) =>
        summary.GetProperty("byMonth").EnumerateArray()
            .Select(m => (m.GetProperty("month").GetString()!, m.GetProperty("cost").GetDecimal()))
            .ToArray();
}
