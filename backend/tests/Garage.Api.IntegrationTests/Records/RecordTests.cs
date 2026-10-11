using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Records;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Garage.Api.IntegrationTests.Records;

[Collection("api")]
public sealed class RecordTests(GarageApiFactory factory)
{
    // 15:00 UTC é 12:00 em São Paulo: "hoje" é 2026-10-10 em todos os testes, salvo o do fim da noite.
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Add_record_with_parts_returns_201_and_detail_has_totals()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var body = TestData.Record(
                laborCost: 80.00m,
                parts:
                [
                    TestData.Part("Óleo 5W30", quantity: 2m, unit: "liter", unitPrice: 45.90m, brand: " Mobil ", partNumber: "  "),
                    TestData.Part("Filtro de óleo", quantity: 1m, unitPrice: 120.00m, partNumber: "PH6017A"),
                    TestData.Part("Arruela do cárter", quantity: 1m)
                ])
            .With("description", "  Óleo sintético e filtro.  ")
            .With("performedBy", " Oficina do Zé ");

        using var created = await client.PostAsJsonAsync(RecordsUrl(vehicleId), body, TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
        Assert.Equal($"{RecordsUrl(vehicleId)}/{id}", created.Headers.Location?.OriginalString);

        var record = await client.GetRecordAsync(vehicleId, id);
        Assert.Equal(id, record.GetProperty("id").GetGuid());
        Assert.Equal("maintenance", record.GetProperty("type").GetString());
        Assert.Equal("2026-10-01", record.GetProperty("date").GetString());
        Assert.Equal(85_000, record.GetProperty("odometer").GetInt32());
        Assert.Equal("Troca de óleo", record.GetProperty("title").GetString());
        Assert.Equal("Óleo sintético e filtro.", record.GetProperty("description").GetString());
        Assert.Equal("engine", record.GetProperty("category").GetString());
        Assert.Equal("Oficina do Zé", record.GetProperty("performedBy").GetString());
        Assert.Equal(80.00m, record.GetProperty("laborCost").GetDecimal());
        Assert.Equal(JsonValueKind.Null, record.GetProperty("replacedOriginalPart").ValueKind);
        Assert.Equal(291.80m, record.GetProperty("totalCost").GetDecimal());
        Assert.True(RowVersion(record) > 0);

        var parts = record.GetProperty("parts").EnumerateArray().ToDictionary(part => part.GetProperty("name").GetString()!);
        Assert.Equal(3, parts.Count);
        var oil = parts["Óleo 5W30"];
        Assert.Equal("Mobil", oil.GetProperty("brand").GetString());
        Assert.Equal(JsonValueKind.Null, oil.GetProperty("partNumber").ValueKind);
        Assert.Equal(2m, oil.GetProperty("quantity").GetDecimal());
        Assert.Equal("liter", oil.GetProperty("unit").GetString());
        Assert.Equal(45.90m, oil.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(91.80m, oil.GetProperty("lineTotal").GetDecimal());
        var filter = parts["Filtro de óleo"];
        Assert.Equal(JsonValueKind.Null, filter.GetProperty("brand").ValueKind);
        Assert.Equal("PH6017A", filter.GetProperty("partNumber").GetString());
        Assert.Equal("unit", filter.GetProperty("unit").GetString());
        Assert.Equal(120.00m, filter.GetProperty("lineTotal").GetDecimal());
        // Sem preço, a peça entra com total zero.
        var washer = parts["Arruela do cárter"];
        Assert.Equal(JsonValueKind.Null, washer.GetProperty("unitPrice").ValueKind);
        Assert.Equal(0m, washer.GetProperty("lineTotal").GetDecimal());

        // A lista traz o mesmo total.
        var item = Assert.Single(Items(await client.GetRecordsAsync(vehicleId)));
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal(291.80m, item.GetProperty("totalCost").GetDecimal());
    }

    [Fact]
    public async Task Parts_come_back_in_the_order_they_were_entered_also_after_reordering()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        // Ordem que não é a alfabética nem a de criação dos ids (v7 no mesmo milissegundo têm ordem aleatória).
        string[] entered = ["Óleo 5W30", "Filtro de óleo", "Arruela do cárter"];
        var id = await client.CreateRecordAsync(
            vehicleId, TestData.Record(parts: entered.Select(name => (object)TestData.Part(name)).ToArray()));

        Assert.Equal(entered, PartNames(await client.GetRecordAsync(vehicleId, id)));

        string[] reordered = ["Arruela do cárter", "Óleo 5W30", "Filtro de óleo"];
        var rowVersion = RowVersion(await client.GetRecordAsync(vehicleId, id));
        using var updated = await client.PutAsJsonAsync(
            $"{RecordsUrl(vehicleId)}/{id}",
            TestData.Record(parts: reordered.Select(name => (object)TestData.Part(name)).ToArray()).With("rowVersion", rowVersion),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal(reordered, PartNames(await client.GetRecordAsync(vehicleId, id)));
    }

    [Fact]
    public async Task Add_record_with_higher_km_updates_vehicle_odometer()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 150_000));

        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 10, 1), odometer: 151_000));
        Assert.Equal(151_000, await CurrentOdometerAsync(client, vehicleId));

        // Uma leitura mais antiga e mais baixa não volta a km atual (V1).
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 9, 1), odometer: 149_000));
        Assert.Equal(151_000, await CurrentOdometerAsync(client, vehicleId));
    }

    [Fact]
    public async Task Add_record_out_of_order_returns_422_with_bounds()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 70_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 1, 10), odometer: 80_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 6, 10), odometer: 90_000));

        using var response = await client.PostAsJsonAsync(
            RecordsUrl(vehicleId), TestData.Record(date: new DateOnly(2026, 3, 10), odometer: 95_000), TestJson.Options);

        var problem = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "record.odometer_out_of_order");
        Assert.Equal(80_000, problem.GetProperty("maxKmBefore").GetInt32());
        Assert.Equal(90_000, problem.GetProperty("minKmAfter").GetInt32());
        Assert.Equal(2, Items(await client.GetRecordsAsync(vehicleId)).Count);
        // A leitura de km só vale se o registro for criado (R4).
        Assert.Equal(90_000, await CurrentOdometerAsync(client, vehicleId));

        // Os limites são inclusivos e registros na mesma data não se restringem: 90.000 e depois 80.000 em 10/03.
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 3, 10), odometer: 90_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 3, 10), odometer: 80_000));
        Assert.Equal(4, Items(await client.GetRecordsAsync(vehicleId)).Count);
    }

    [Fact]
    public async Task Add_old_record_without_km_is_accepted()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 90_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 1, 10), odometer: 80_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 6, 10), odometer: 90_000));

        var old = await client.CreateRecordAsync(
            vehicleId, TestData.Record(title: "Troca da correia", date: new DateOnly(2019, 5, 20), odometer: null));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 3, 10), odometer: null));
        // Registros sem km não restringem os outros: 85.000 em 11/03 só olha 10/01 (80.000) e 10/06 (90.000).
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 3, 11), odometer: 85_000));

        var record = await client.GetRecordAsync(vehicleId, old);
        Assert.Equal("2019-05-20", record.GetProperty("date").GetString());
        Assert.Equal(JsonValueKind.Null, record.GetProperty("odometer").ValueKind);
        Assert.Equal(5, Items(await client.GetRecordsAsync(vehicleId)).Count);
        Assert.Equal(90_000, await CurrentOdometerAsync(client, vehicleId));
    }

    [Fact]
    public async Task Add_record_fulfilling_reminder_moves_its_base()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 80_000));
        var reminderId = await client.CreateReminderAsync(
            vehicleId, TestData.Reminder(lastDoneDate: new DateOnly(2026, 1, 1), lastDoneOdometer: 75_000));
        var notFulfilled = await client.CreateReminderAsync(vehicleId, MonthlyReminder("Fluido de freio"));

        var recordId = await client.CreateRecordAsync(vehicleId, TestData.Record(
            date: new DateOnly(2026, 10, 1), odometer: 85_000, fulfilledReminderIds: [reminderId]));

        var reminder = await client.GetReminderAsync(vehicleId, reminderId);
        Assert.Equal("2026-10-01", reminder.GetProperty("lastDoneDate").GetString());
        Assert.Equal(85_000, reminder.GetProperty("lastDoneOdometer").GetInt32());
        Assert.Equal("2026-01-01", (await client.GetReminderAsync(vehicleId, notFulfilled)).GetProperty("lastDoneDate").GetString());
        Assert.Equal(85_000, await CurrentOdometerAsync(client, vehicleId));

        // R6: editar o registro não mexe no lembrete.
        var rowVersion = RowVersion(await client.GetRecordAsync(vehicleId, recordId));
        using var edited = await client.PutAsJsonAsync(
            $"{RecordsUrl(vehicleId)}/{recordId}",
            TestData.Record(date: new DateOnly(2026, 10, 5), odometer: 86_000).With("rowVersion", rowVersion),
            TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);
        var afterEdit = await client.GetReminderAsync(vehicleId, reminderId);
        Assert.Equal("2026-10-01", afterEdit.GetProperty("lastDoneDate").GetString());
        Assert.Equal(85_000, afterEdit.GetProperty("lastDoneOdometer").GetInt32());
    }

    [Fact]
    public async Task Fulfilling_km_reminder_without_record_km_returns_422_and_creates_nothing()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 80_000));
        // O lembrete por meses vem antes na lista e seria cumprido; a falha do de km precisa desfazer tudo.
        var monthly = await client.CreateReminderAsync(vehicleId, MonthlyReminder());
        var byKm = await client.CreateReminderAsync(
            vehicleId, TestData.Reminder(lastDoneDate: new DateOnly(2026, 1, 1), lastDoneOdometer: 75_000));
        var monthlyBefore = await client.GetReminderAsync(vehicleId, monthly);
        var byKmBefore = await client.GetReminderAsync(vehicleId, byKm);

        using var response = await client.PostAsJsonAsync(
            RecordsUrl(vehicleId),
            TestData.Record(date: new DateOnly(2026, 10, 1), odometer: null, fulfilledReminderIds: [monthly, byKm]),
            TestJson.Options);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "reminder.fulfillment_requires_odometer");
        Assert.Empty(Items(await client.GetRecordsAsync(vehicleId)));
        AssertUnchanged(monthlyBefore, await client.GetReminderAsync(vehicleId, monthly));
        AssertUnchanged(byKmBefore, await client.GetReminderAsync(vehicleId, byKm));
    }

    [Fact]
    public async Task Fulfilling_reminder_with_record_older_than_its_base_returns_422()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 80_000));
        var reminderId = await client.CreateReminderAsync(vehicleId, TestData.Reminder(lastDoneDate: new DateOnly(2026, 9, 1)));
        var before = await client.GetReminderAsync(vehicleId, reminderId);

        using var response = await client.PostAsJsonAsync(
            RecordsUrl(vehicleId),
            TestData.Record(date: new DateOnly(2026, 8, 1), odometer: 79_000, fulfilledReminderIds: [reminderId]),
            TestJson.Options);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "reminder.fulfillment_older_than_base");
        Assert.Empty(Items(await client.GetRecordsAsync(vehicleId)));
        AssertUnchanged(before, await client.GetReminderAsync(vehicleId, reminderId));
    }

    [Fact]
    public async Task AddRecord_with_reminder_of_other_vehicle_returns_404_and_changes_nothing()
    {
        factory.Clock.SetUtcNow(Now);
        using var owner = await factory.CreateUserClientAsync();
        using var other = await factory.CreateUserClientAsync();
        var vehicleId = await owner.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 150_000));
        var ownReminder = await owner.CreateReminderAsync(vehicleId, MonthlyReminder());
        var siblingVehicle = await owner.CreateVehicleAsync();
        var siblingReminder = await owner.CreateReminderAsync(siblingVehicle, MonthlyReminder());
        var foreignVehicle = await other.CreateVehicleAsync();
        var foreignReminder = await other.CreateReminderAsync(foreignVehicle, MonthlyReminder());
        var ownBefore = await owner.GetReminderAsync(vehicleId, ownReminder);
        var siblingBefore = await owner.GetReminderAsync(siblingVehicle, siblingReminder);
        var foreignBefore = await other.GetReminderAsync(foreignVehicle, foreignReminder);

        // Todos seriam cumpríveis (só meses, base antiga): o único motivo da recusa é não serem do veículo do registro.
        Guid[][] attempts = [[ownReminder, foreignReminder], [siblingReminder], [Guid.NewGuid()]];
        foreach (var reminderIds in attempts)
        {
            using var response = await owner.PostAsJsonAsync(
                RecordsUrl(vehicleId),
                TestData.Record(date: new DateOnly(2026, 10, 1), odometer: 160_000, fulfilledReminderIds: reminderIds),
                TestJson.Options);

            await response.AssertProblemAsync(HttpStatusCode.NotFound, "reminder.not_found");
        }

        Assert.Empty(Items(await owner.GetRecordsAsync(vehicleId)));
        Assert.Equal(150_000, await CurrentOdometerAsync(owner, vehicleId));
        AssertUnchanged(ownBefore, await owner.GetReminderAsync(vehicleId, ownReminder));
        AssertUnchanged(siblingBefore, await owner.GetReminderAsync(siblingVehicle, siblingReminder));
        AssertUnchanged(foreignBefore, await other.GetReminderAsync(foreignVehicle, foreignReminder));
    }

    [Fact]
    public async Task Record_dated_local_today_late_night_is_accepted_and_local_tomorrow_rejected()
    {
        // 02:30 UTC de 11/10 ainda é 23:30 de 10/10 em São Paulo. Relógio próprio: o da fábrica é compartilhado com os
        // outros testes e não volta no tempo.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 2, 30, 0, TimeSpan.Zero));
        using var lateNight = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = await lateNight.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        using var today = await client.PostAsJsonAsync(
            RecordsUrl(vehicleId), TestData.Record(date: new DateOnly(2026, 10, 10)), TestJson.Options);
        using var tomorrow = await client.PostAsJsonAsync(
            RecordsUrl(vehicleId), TestData.Record(date: new DateOnly(2026, 10, 11)), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, today.StatusCode);
        await tomorrow.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "record.date_in_future");
        var item = Assert.Single(Items(await client.GetRecordsAsync(vehicleId)));
        Assert.Equal("2026-10-10", item.GetProperty("date").GetString());
    }

    [Fact]
    public async Task Moving_record_date_revalidates_bounds_excluding_itself()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 70_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(title: "A", date: new DateOnly(2026, 1, 10), odometer: 80_000));
        var b = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "B", date: new DateOnly(2026, 6, 10), odometer: 90_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(title: "C", date: new DateOnly(2026, 8, 10), odometer: 95_000));
        var url = $"{RecordsUrl(vehicleId)}/{b}";
        var rowVersion = RowVersion(await client.GetRecordAsync(vehicleId, b));

        using var moved = await client.PutAsJsonAsync(
            url, TestData.Record(title: "B", date: new DateOnly(2026, 9, 10), odometer: 90_000).With("rowVersion", rowVersion), TestJson.Options);

        var problem = await moved.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "record.odometer_out_of_order");
        Assert.Equal(95_000, problem.GetProperty("maxKmBefore").GetInt32());
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("minKmAfter").ValueKind);
        var unchanged = await client.GetRecordAsync(vehicleId, b);
        Assert.Equal("2026-06-10", unchanged.GetProperty("date").GetString());
        Assert.Equal(rowVersion, RowVersion(unchanged));

        using var renamed = await client.PutAsJsonAsync(
            url, TestData.Record(title: "B revisado", date: new DateOnly(2026, 6, 10), odometer: 90_000).With("rowVersion", rowVersion), TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        var afterRename = await client.GetRecordAsync(vehicleId, b);
        Assert.Equal("B revisado", afterRename.GetProperty("title").GetString());

        // Fora do cálculo, o próprio B (90.000 km em 10/06) não impede movê-lo para 10/07 com 85.000 km.
        using var movedLower = await client.PutAsJsonAsync(
            url,
            TestData.Record(title: "B revisado", date: new DateOnly(2026, 7, 10), odometer: 85_000).With("rowVersion", RowVersion(afterRename)),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, movedLower.StatusCode);
        var afterMove = await client.GetRecordAsync(vehicleId, b);
        Assert.Equal("2026-07-10", afterMove.GetProperty("date").GetString());
        Assert.Equal(85_000, afterMove.GetProperty("odometer").GetInt32());
    }

    [Fact]
    public async Task Deleting_highest_km_record_keeps_current_km_and_correction_fixes_typo()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 15_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 9, 1), odometer: 15_200));
        var typo = await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 10, 1), odometer: 150_000));
        Assert.Equal(150_000, await CurrentOdometerAsync(client, vehicleId));

        using var deleted = await client.DeleteAsync($"{RecordsUrl(vehicleId)}/{typo}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        // V4: excluir o registro não diminui a km atual.
        Assert.Equal(150_000, await CurrentOdometerAsync(client, vehicleId));

        using var belowRecords = await client.PostAsJsonAsync(CorrectionUrl(vehicleId), new { km = 14_000 }, TestJson.Options);

        var problem = await belowRecords.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "vehicle.odometer_below_records");
        Assert.Equal(15_200, problem.GetProperty("maxRecordKm").GetInt32());
        Assert.Equal(150_000, await CurrentOdometerAsync(client, vehicleId));

        using var corrected = await client.PostAsJsonAsync(CorrectionUrl(vehicleId), new { km = 15_200 }, TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, corrected.StatusCode);
        Assert.Equal(15_200, await CurrentOdometerAsync(client, vehicleId));
    }

    [Fact]
    public async Task Correction_without_records_accepts_any_valid_km()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 150_000));
        // Registros sem km não contam para o mínimo da correção.
        await client.CreateRecordAsync(vehicleId, TestData.Record(odometer: null));

        using var corrected = await client.PostAsJsonAsync(CorrectionUrl(vehicleId), new { km = 15_000 }, TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, corrected.StatusCode);
        Assert.Equal(15_000, await CurrentOdometerAsync(client, vehicleId));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2_000_001)]
    public async Task Correction_out_of_range_returns_400(int km)
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        using var response = await client.PostAsJsonAsync(CorrectionUrl(vehicleId), new { km }, TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "km" }, FieldNames(errors));
    }

    [Fact]
    public async Task Invalid_cursor_returns_400_validation()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        string[] cursors =
        [
            "@@@",
            Base64Url("não é um cursor"),
            Base64Url($"2026-10-01|ontem|{Guid.NewGuid()}"),
            Base64Url($"2026-13-01|{Now:O}|{Guid.NewGuid()}")
        ];

        foreach (var cursor in cursors)
        {
            using var response = await client.GetAsync($"{RecordsUrl(vehicleId)}?cursor={Uri.EscapeDataString(cursor)}");

            var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
            Assert.Equal(new[] { "cursor" }, FieldNames(errors));
            Assert.Equal("Cursor inválido.", errors.GetProperty("cursor")[0].GetString());
        }

        foreach (var pageSize in new[] { 0, 101 })
        {
            using var response = await client.GetAsync($"{RecordsUrl(vehicleId)}?pageSize={pageSize}");

            var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
            Assert.Equal(new[] { "pageSize" }, FieldNames(errors));
        }
    }

    [Fact]
    public async Task Pagination_returns_every_record_once_in_date_desc_order()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var january = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Janeiro", date: new DateOnly(2026, 1, 10), odometer: null));
        var july = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Julho", date: new DateOnly(2026, 7, 10), odometer: null));
        var mayFirst = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Maio 1", date: new DateOnly(2026, 5, 10), odometer: null));
        var march = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Março", date: new DateOnly(2026, 3, 10), odometer: null));
        var maySecond = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Maio 2", date: new DateOnly(2026, 5, 10), odometer: null));
        // Mesma data e mesmo instante de criação (relógio fixo): desempata o id, do maior para o menor (a ordem do uuid no
        // Postgres é a do texto). Com páginas de 2, o par de maio fica dividido entre a 1ª e a 2ª página.
        var may = new[] { mayFirst, maySecond }.OrderByDescending(id => id.ToString(), StringComparer.Ordinal).ToArray();
        Guid[] expected = [july, may[0], may[1], march, january];

        List<Guid> seen = [];
        List<int> pageSizes = [];
        string? cursor = null;
        do
        {
            var query = cursor is null ? "pageSize=2" : $"pageSize=2&cursor={Uri.EscapeDataString(cursor)}";
            var page = await client.GetRecordsAsync(vehicleId, query);
            var items = Items(page);
            pageSizes.Add(items.Count);
            seen.AddRange(items.Select(item => item.GetProperty("id").GetGuid()));
            cursor = page.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null && pageSizes.Count < 10);

        Assert.Equal(new[] { 2, 2, 1 }, pageSizes);
        Assert.Equal(expected, seen);

        // Página exatamente do tamanho do que resta: sem próxima página. Sem pageSize, vale o padrão (30).
        var exact = await client.GetRecordsAsync(vehicleId, "pageSize=5");
        Assert.Equal(expected, Items(exact).Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal(JsonValueKind.Null, exact.GetProperty("nextCursor").ValueKind);
        var all = await client.GetRecordsAsync(vehicleId);
        Assert.Equal(5, Items(all).Count);
        Assert.Equal(JsonValueKind.Null, all.GetProperty("nextCursor").ValueKind);

        var first = Items(all)[0];
        Assert.Equal("maintenance", first.GetProperty("type").GetString());
        Assert.Equal("2026-07-10", first.GetProperty("date").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("odometer").ValueKind);
        Assert.Equal("Julho", first.GetProperty("title").GetString());
        Assert.Equal("engine", first.GetProperty("category").GetString());
        Assert.Equal(0m, first.GetProperty("totalCost").GetDecimal());
    }

    [Fact]
    public async Task Filters_by_type_and_category()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var oil = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Óleo", type: "maintenance", category: "engine"));
        var pads = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Pastilhas", type: "maintenance", category: "brakes"));
        var intake = await client.CreateRecordAsync(vehicleId, TestData.Record(
            title: "Admissão", type: "modification", category: "engine", replacedOriginalPart: "Filtro de ar original"));

        Assert.Equal(Sorted(oil, pads), await IdsAsync(client, vehicleId, "type=maintenance"));
        Assert.Equal(Sorted(oil, intake), await IdsAsync(client, vehicleId, "category=engine"));
        Assert.Equal(Sorted(intake), await IdsAsync(client, vehicleId, "type=modification&category=engine"));
        // Os valores vêm como no JSON (camelCase), mas maiúsculas não importam.
        Assert.Equal(Sorted(pads), await IdsAsync(client, vehicleId, "type=MAINTENANCE&category=Brakes"));
        Assert.Empty(await IdsAsync(client, vehicleId, "category=wheelsTires"));

        foreach (var (query, field) in new[] { ("type=repair", "type"), ("category=99", "category") })
        {
            using var response = await client.GetAsync($"{RecordsUrl(vehicleId)}?{query}");

            var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
            Assert.Equal(new[] { field }, FieldNames(errors));
        }
    }

    [Fact]
    public async Task Replaced_part_on_maintenance_returns_422()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        using var rejected = await client.PostAsJsonAsync(
            RecordsUrl(vehicleId), TestData.Record(type: "maintenance", replacedOriginalPart: "Escapamento original"), TestJson.Options);

        await rejected.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "record.replaced_part_only_for_modification");
        Assert.Empty(Items(await client.GetRecordsAsync(vehicleId)));

        // Em branco conta como ausente; numa modificação o texto é aceito (aparado).
        var blank = await client.CreateRecordAsync(vehicleId, TestData.Record(type: "maintenance", replacedOriginalPart: "   "));
        var modification = await client.CreateRecordAsync(
            vehicleId, TestData.Record(type: "modification", replacedOriginalPart: " Escapamento original "));

        Assert.Equal(JsonValueKind.Null, (await client.GetRecordAsync(vehicleId, blank)).GetProperty("replacedOriginalPart").ValueKind);
        var modified = await client.GetRecordAsync(vehicleId, modification);
        Assert.Equal("modification", modified.GetProperty("type").GetString());
        Assert.Equal("Escapamento original", modified.GetProperty("replacedOriginalPart").GetString());
    }

    [Fact]
    public async Task Title_longer_than_120_returns_400()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        foreach (var title in new[] { new string('a', 121), "", "   " })
        {
            using var response = await client.PostAsJsonAsync(RecordsUrl(vehicleId), TestData.Record(title: title), TestJson.Options);

            var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
            Assert.Equal(new[] { "title" }, FieldNames(errors));
        }

        var longest = await client.CreateRecordAsync(vehicleId, TestData.Record(title: new string('a', 120)));
        var padded = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "  Revisão dos 90 mil "));
        Assert.Equal(new string('a', 120), (await client.GetRecordAsync(vehicleId, longest)).GetProperty("title").GetString());
        Assert.Equal("Revisão dos 90 mil", (await client.GetRecordAsync(vehicleId, padded)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invalid_fields_return_400_with_camel_case_keys_per_segment()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var body = TestData.Record(
                odometer: 2_000_001,
                laborCost: 10.001m,
                parts:
                [
                    TestData.Part(name: " "),
                    TestData.Part(quantity: 0m),
                    TestData.Part(quantity: 1.0001m),
                    TestData.Part(quantity: 100_000m),
                    TestData.Part(unitPrice: -1m),
                    TestData.Part(unitPrice: 1.001m, brand: new string('b', 61), partNumber: new string('p', 61)).With("unit", 99)
                ],
                fulfilledReminderIds: Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToArray())
            .With("type", 99)
            .With("category", 99)
            .With("description", new string('d', 4001))
            .With("performedBy", new string('p', 121))
            .With("replacedOriginalPart", new string('r', 501));

        using var response = await client.PostAsJsonAsync(RecordsUrl(vehicleId), body, TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(
            new[]
            {
                "category", "description", "fulfilledReminderIds", "laborCost", "odometer",
                "parts[0].name", "parts[1].quantity", "parts[2].quantity", "parts[3].quantity", "parts[4].unitPrice",
                "parts[5].brand", "parts[5].partNumber", "parts[5].unit", "parts[5].unitPrice",
                "performedBy", "replacedOriginalPart", "type"
            },
            FieldNames(errors));

        var tooManyParts = Enumerable.Range(1, 51).Select(i => (object)TestData.Part($"Peça {i}")).ToArray();
        using var tooMany = await client.PostAsJsonAsync(RecordsUrl(vehicleId), TestData.Record(parts: tooManyParts), TestJson.Options);

        var tooManyErrors = (await tooMany.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "parts" }, FieldNames(tooManyErrors));

        using var nullPart = await client.PostAsJsonAsync(RecordsUrl(vehicleId), TestData.Record(parts: [null!]), TestJson.Options);

        var nullPartErrors = (await nullPart.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "parts[0]" }, FieldNames(nullPartErrors));
        Assert.Empty(Items(await client.GetRecordsAsync(vehicleId)));
    }

    [Fact]
    public async Task Parts_and_fulfilled_reminders_may_be_omitted()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var body = TestData.Record(laborCost: 150m);
        body.Remove("parts");
        body.Remove("fulfilledReminderIds");

        var id = await client.CreateRecordAsync(vehicleId, body);

        var record = await client.GetRecordAsync(vehicleId, id);
        Assert.Equal(0, record.GetProperty("parts").GetArrayLength());
        Assert.Equal(150m, record.GetProperty("totalCost").GetDecimal());
    }

    [Fact]
    public async Task Money_totals_above_the_limit_return_400_instead_of_500()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        // Preço unitário válido, mas 2 × 9.999.999.999,99 passa do limite do total da peça.
        using var line = await client.PostAsJsonAsync(
            RecordsUrl(vehicleId), TestData.Record(parts: [TestData.Part(quantity: 2m, unitPrice: 9_999_999_999.99m)]), TestJson.Options);
        // Cada peça e a mão de obra cabem; a soma do registro não.
        using var total = await client.PostAsJsonAsync(
            RecordsUrl(vehicleId),
            TestData.Record(laborCost: 0.01m, parts: [TestData.Part(unitPrice: 9_999_999_999.99m)]),
            TestJson.Options);

        var lineErrors = (await line.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "parts[0].unitPrice" }, FieldNames(lineErrors));
        var totalErrors = (await total.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "totalCost" }, FieldNames(totalErrors));
        Assert.Empty(Items(await client.GetRecordsAsync(vehicleId)));

        // Exatamente no limite é aceito, na gravação e na leitura.
        var id = await client.CreateRecordAsync(
            vehicleId, TestData.Record(laborCost: 0.99m, parts: [TestData.Part(unitPrice: 9_999_999_999.00m)]));
        Assert.Equal(9_999_999_999.99m, (await client.GetRecordAsync(vehicleId, id)).GetProperty("totalCost").GetDecimal());
        Assert.Equal(9_999_999_999.99m, Assert.Single(Items(await client.GetRecordsAsync(vehicleId))).GetProperty("totalCost").GetDecimal());
    }

    [Fact]
    public async Task Update_replaces_fields_and_parts_and_registers_km()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 80_000));
        var id = await client.CreateRecordAsync(vehicleId, TestData.Record(
            laborCost: 30m, parts: [TestData.Part("Filtro velho", unitPrice: 50m), TestData.Part("Junta", unitPrice: 5m)]));
        var before = RowVersion(await client.GetRecordAsync(vehicleId, id));

        var body = TestData.Record(
                title: " Molas esportivas ",
                type: "modification",
                category: "suspension",
                date: new DateOnly(2026, 10, 5),
                odometer: 88_000,
                replacedOriginalPart: "Molas originais",
                parts: [TestData.Part("Mola dianteira", quantity: 2m, unitPrice: 450m), TestData.Part("Mola traseira", quantity: 2m, unitPrice: 400m)])
            .With("description", "Rebaixamento de 30 mm")
            .With("performedBy", "Eu")
            .With("rowVersion", before);
        using var response = await client.PutAsJsonAsync($"{RecordsUrl(vehicleId)}/{id}", body, TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var record = await client.GetRecordAsync(vehicleId, id);
        Assert.Equal("Molas esportivas", record.GetProperty("title").GetString());
        Assert.Equal("modification", record.GetProperty("type").GetString());
        Assert.Equal("suspension", record.GetProperty("category").GetString());
        Assert.Equal("2026-10-05", record.GetProperty("date").GetString());
        Assert.Equal(88_000, record.GetProperty("odometer").GetInt32());
        Assert.Equal("Rebaixamento de 30 mm", record.GetProperty("description").GetString());
        Assert.Equal("Eu", record.GetProperty("performedBy").GetString());
        Assert.Equal(JsonValueKind.Null, record.GetProperty("laborCost").ValueKind);
        Assert.Equal("Molas originais", record.GetProperty("replacedOriginalPart").GetString());
        Assert.Equal(
            new[] { "Mola dianteira", "Mola traseira" },
            record.GetProperty("parts").EnumerateArray().Select(part => part.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.Equal(1_700m, record.GetProperty("totalCost").GetDecimal());
        Assert.NotEqual(before, RowVersion(record));
        // As peças antigas saem do banco (não ficam órfãs ao lado das novas).
        Assert.Equal(2, await PartRowsAsync(id));
        // R4: a km do registro editado é uma leitura do hodômetro.
        Assert.Equal(88_000, await CurrentOdometerAsync(client, vehicleId));
    }

    [Fact]
    public async Task Update_with_stale_row_version_returns_409()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var id = await client.CreateRecordAsync(vehicleId);
        var url = $"{RecordsUrl(vehicleId)}/{id}";
        var stale = RowVersion(await client.GetRecordAsync(vehicleId, id));

        using var first = await client.PutAsJsonAsync(url, TestData.Record(title: "Primeiro").With("rowVersion", stale), TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        var current = RowVersion(await client.GetRecordAsync(vehicleId, id));
        Assert.NotEqual(stale, current);

        using var second = await client.PutAsJsonAsync(url, TestData.Record(title: "Segundo").With("rowVersion", stale), TestJson.Options);

        await second.AssertProblemAsync(HttpStatusCode.Conflict, "concurrency.conflict");
        Assert.Equal("Primeiro", (await client.GetRecordAsync(vehicleId, id)).GetProperty("title").GetString());

        // Com a versão atual o mesmo PUT passa.
        using var retry = await client.PutAsJsonAsync(url, TestData.Record(title: "Segundo").With("rowVersion", current), TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.Equal("Segundo", (await client.GetRecordAsync(vehicleId, id)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Update_changing_only_parts_also_checks_and_advances_the_row_version()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var id = await client.CreateRecordAsync(vehicleId, TestData.Record(parts: [TestData.Part("Original")]));
        var url = $"{RecordsUrl(vehicleId)}/{id}";
        var stale = RowVersion(await client.GetRecordAsync(vehicleId, id));

        // As peças ficam em outra tabela: mudar só elas também precisa avançar a versão do registro...
        using var first = await client.PutAsJsonAsync(
            url, TestData.Record(parts: [TestData.Part("Primeira")]).With("rowVersion", stale), TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.NotEqual(stale, RowVersion(await client.GetRecordAsync(vehicleId, id)));

        // ...e conferi-la, senão uma tela antiga apagaria as peças gravadas por outra.
        using var second = await client.PutAsJsonAsync(
            url, TestData.Record(parts: [TestData.Part("Segunda")]).With("rowVersion", stale), TestJson.Options);

        await second.AssertProblemAsync(HttpStatusCode.Conflict, "concurrency.conflict");
        var part = Assert.Single((await client.GetRecordAsync(vehicleId, id)).GetProperty("parts").EnumerateArray());
        Assert.Equal("Primeira", part.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Delete_removes_the_record_with_its_parts_and_deleting_the_vehicle_removes_its_records()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var kept = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Fica", parts: [TestData.Part("Peça que fica")]));
        var removed = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Sai", parts: [TestData.Part("Peça que sai")]));
        var url = $"{RecordsUrl(vehicleId)}/{removed}";

        using var deleted = await client.DeleteAsync(url);
        using var get = await client.GetAsync(url);
        using var deletedAgain = await client.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await get.AssertProblemAsync(HttpStatusCode.NotFound, "record.not_found");
        await deletedAgain.AssertProblemAsync(HttpStatusCode.NotFound, "record.not_found");
        Assert.Equal(kept, Assert.Single(Items(await client.GetRecordsAsync(vehicleId))).GetProperty("id").GetGuid());
        Assert.Equal(0, await PartRowsAsync(removed));
        Assert.Equal(1, await PartRowsAsync(kept));

        using var vehicleDeleted = await client.DeleteAsync($"/api/vehicles/{vehicleId}");

        Assert.Equal(HttpStatusCode.NoContent, vehicleDeleted.StatusCode);
        Assert.False(await AnyRecordAsync(vehicleId));
        Assert.Equal(0, await PartRowsAsync(kept));
    }

    [Fact]
    public async Task Other_user_gets_404_on_records_and_correction()
    {
        factory.Clock.SetUtcNow(Now);
        using var owner = await factory.CreateUserClientAsync();
        using var other = await factory.CreateUserClientAsync();
        var vehicleId = await owner.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 150_000));
        var id = await owner.CreateRecordAsync(vehicleId, TestData.Record(parts: [TestData.Part()]));
        var rowVersion = RowVersion(await owner.GetRecordAsync(vehicleId, id));
        var url = RecordsUrl(vehicleId);

        using var list = await other.GetAsync(url);
        using var get = await other.GetAsync($"{url}/{id}");
        using var post = await other.PostAsJsonAsync(url, TestData.Record(odometer: 200_000), TestJson.Options);
        using var put = await other.PutAsJsonAsync(
            $"{url}/{id}", TestData.Record(title: "Roubado").With("rowVersion", rowVersion), TestJson.Options);
        using var delete = await other.DeleteAsync($"{url}/{id}");
        using var correction = await other.PostAsJsonAsync(CorrectionUrl(vehicleId), new { km = 200_000 }, TestJson.Options);

        await list.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await get.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await post.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await put.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await delete.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await correction.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        var untouched = await owner.GetRecordAsync(vehicleId, id);
        Assert.Equal("Troca de óleo", untouched.GetProperty("title").GetString());
        Assert.Equal(rowVersion, RowVersion(untouched));
        Assert.Single(Items(await owner.GetRecordsAsync(vehicleId)));
        Assert.Equal(150_000, await CurrentOdometerAsync(owner, vehicleId));
    }

    [Fact]
    public async Task Record_of_another_vehicle_or_unknown_gets_404()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleA = await client.CreateVehicleAsync();
        var vehicleB = await client.CreateVehicleAsync();
        var id = await client.CreateRecordAsync(vehicleA);
        var rowVersion = RowVersion(await client.GetRecordAsync(vehicleA, id));

        // Os filhos são sempre buscados por veículo e id: o registro de A não é alcançável pelo veículo B.
        using var getThroughB = await client.GetAsync($"{RecordsUrl(vehicleB)}/{id}");
        using var putThroughB = await client.PutAsJsonAsync(
            $"{RecordsUrl(vehicleB)}/{id}", TestData.Record(title: "Trocado").With("rowVersion", rowVersion), TestJson.Options);
        using var deleteThroughB = await client.DeleteAsync($"{RecordsUrl(vehicleB)}/{id}");
        using var getUnknown = await client.GetAsync($"{RecordsUrl(vehicleA)}/{Guid.NewGuid()}");
        using var putUnknown = await client.PutAsJsonAsync(
            $"{RecordsUrl(vehicleA)}/{Guid.NewGuid()}", TestData.Record().With("rowVersion", rowVersion), TestJson.Options);

        await getThroughB.AssertProblemAsync(HttpStatusCode.NotFound, "record.not_found");
        await putThroughB.AssertProblemAsync(HttpStatusCode.NotFound, "record.not_found");
        await deleteThroughB.AssertProblemAsync(HttpStatusCode.NotFound, "record.not_found");
        await getUnknown.AssertProblemAsync(HttpStatusCode.NotFound, "record.not_found");
        await putUnknown.AssertProblemAsync(HttpStatusCode.NotFound, "record.not_found");
        var untouched = await client.GetRecordAsync(vehicleA, id);
        Assert.Equal("Troca de óleo", untouched.GetProperty("title").GetString());
        Assert.Empty(Items(await client.GetRecordsAsync(vehicleB)));
    }

    [Fact]
    public async Task Repository_bounds_and_max_use_only_records_with_km_of_the_vehicle()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 70_000));
        var otherVehicle = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 70_000));
        var empty = await client.CreateVehicleAsync();
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 1, 10), odometer: 80_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 3, 10), odometer: 85_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 3, 10), odometer: null));
        var april = await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 4, 10), odometer: 87_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 6, 10), odometer: 90_000));
        await client.CreateRecordAsync(vehicleId, TestData.Record(date: new DateOnly(2026, 7, 10), odometer: null));
        // Outro veículo, com km que cairia dentro dos limites: precisa ficar de fora.
        await client.CreateRecordAsync(otherVehicle, TestData.Record(date: new DateOnly(2026, 2, 10), odometer: 82_000));
        await client.CreateRecordAsync(otherVehicle, TestData.Record(date: new DateOnly(2026, 5, 10), odometer: 99_000));

        await using var scope = factory.Services.CreateAsyncScope();
        var records = scope.ServiceProvider.GetRequiredService<IServiceRecordRepository>();
        var ct = CancellationToken.None;

        // Estritamente antes e estritamente depois: os de 10/03 não contam para 10/03.
        Assert.Equal(new OdometerBounds(80_000, 87_000), await records.GetOdometerBoundsAsync(vehicleId, new DateOnly(2026, 3, 10), null, ct));
        Assert.Equal(new OdometerBounds(85_000, 90_000), await records.GetOdometerBoundsAsync(vehicleId, new DateOnly(2026, 4, 10), april, ct));
        Assert.Equal(new OdometerBounds(90_000, null), await records.GetOdometerBoundsAsync(vehicleId, new DateOnly(2026, 8, 1), null, ct));
        Assert.Equal(new OdometerBounds(null, 80_000), await records.GetOdometerBoundsAsync(vehicleId, new DateOnly(2025, 1, 1), null, ct));
        Assert.Equal(OdometerBounds.None, await records.GetOdometerBoundsAsync(empty, new DateOnly(2026, 3, 10), null, ct));
        Assert.Equal(90_000, await records.GetMaxOdometerAsync(vehicleId, ct));
        Assert.Null(await records.GetMaxOdometerAsync(empty, ct));
    }

    private static string RecordsUrl(Guid vehicleId) => $"/api/vehicles/{vehicleId}/records";

    private static string CorrectionUrl(Guid vehicleId) => $"/api/vehicles/{vehicleId}/odometer/correction";

    /// <summary>Lembrete só por meses (6), com base em 01/01/2026 e sem km: qualquer registro posterior o cumpre.</summary>
    private static Dictionary<string, object?> MonthlyReminder(string title = "Revisão semestral") =>
        TestData.Reminder(title, intervalKm: null, intervalMonths: 6, lastDoneDate: new DateOnly(2026, 1, 1), lastDoneOdometer: null);

    private static List<JsonElement> Items(JsonElement page) => page.GetProperty("items").EnumerateArray().ToList();

    private static uint RowVersion(JsonElement element) => element.GetProperty("rowVersion").GetUInt32();

    private static string?[] PartNames(JsonElement record) =>
        record.GetProperty("parts").EnumerateArray().Select(part => part.GetProperty("name").GetString()).ToArray();

    private static async Task<int> CurrentOdometerAsync(HttpClient client, Guid vehicleId) =>
        (await client.GetVehicleAsync(vehicleId)).GetProperty("currentOdometer").GetInt32();

    private static void AssertUnchanged(JsonElement before, JsonElement after)
    {
        Assert.Equal(before.GetProperty("lastDoneDate").GetString(), after.GetProperty("lastDoneDate").GetString());
        Assert.Equal(before.GetProperty("lastDoneOdometer").ToString(), after.GetProperty("lastDoneOdometer").ToString());
        Assert.Equal(RowVersion(before), RowVersion(after));
    }

    private static async Task<string[]> IdsAsync(HttpClient client, Guid vehicleId, string query) =>
        Items(await client.GetRecordsAsync(vehicleId, query))
            .Select(item => item.GetProperty("id").GetGuid().ToString())
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] Sorted(params Guid[] ids) => ids.Select(id => id.ToString()).Order(StringComparer.Ordinal).ToArray();

    private static string Base64Url(string text) => System.Buffers.Text.Base64Url.EncodeToString(Encoding.UTF8.GetBytes(text));

    private async Task<bool> AnyRecordAsync(Guid vehicleId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IReadDbContext>()
            .Query<ServiceRecord>()
            .AnyAsync(record => record.VehicleId == vehicleId);
    }

    private async Task<int> PartRowsAsync(Guid recordId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<GarageDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM service_record_parts WHERE service_record_id = {recordId}")
            .ToListAsync();
        return rows.Single();
    }

    private static string[] FieldNames(JsonElement errors) =>
        errors.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
}
