using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Api.IntegrationTests.Reminders;

[Collection("api")]
public sealed class ReminderTests(GarageApiFactory factory)
{
    // 15:00 UTC é 12:00 em São Paulo: "hoje" é 2026-10-10 em todos os testes.
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_and_list_with_computed_status()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 84_500));

        using var created = await client.PostAsJsonAsync(RemindersUrl(vehicleId), TestData.Reminder(), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options)).GetProperty("id").GetGuid();
        Assert.Equal($"{RemindersUrl(vehicleId)}/{id}", created.Headers.Location?.OriginalString);

        var reminder = Assert.Single((await client.GetRemindersAsync(vehicleId)).EnumerateArray());
        Assert.Equal(id, reminder.GetProperty("id").GetGuid());
        Assert.Equal("Troca de óleo", reminder.GetProperty("title").GetString());
        Assert.Equal(5_000, reminder.GetProperty("intervalKm").GetInt32());
        Assert.Equal(JsonValueKind.Null, reminder.GetProperty("intervalMonths").ValueKind);
        Assert.Equal("2026-09-01", reminder.GetProperty("lastDoneDate").GetString());
        Assert.Equal(80_000, reminder.GetProperty("lastDoneOdometer").GetInt32());
        Assert.True(reminder.GetProperty("rowVersion").GetUInt32() > 0);
        var status = reminder.GetProperty("status");
        Assert.Equal("dueSoon", status.GetProperty("state").GetString());
        Assert.Equal(500, status.GetProperty("kmRemaining").GetInt32());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("daysRemaining").ValueKind);
        Assert.Equal(Now, status.GetProperty("odometerUpdatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task List_reports_the_worst_axis_of_each_reminder()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 100_000));
        // Só por meses: 2026-04-20 + 6 meses = 2026-10-20, daqui a 10 dias.
        await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            intervalKm: null, intervalMonths: 6, lastDoneDate: new DateOnly(2026, 4, 20), lastDoneOdometer: null));
        // Km em dia (5.000 km restantes), mas o prazo venceu há 9 dias: vale o pior eixo.
        await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            intervalKm: 10_000, intervalMonths: 6, lastDoneDate: new DateOnly(2026, 4, 1), lastDoneOdometer: 95_000));

        var reminders = (await client.GetRemindersAsync(vehicleId)).EnumerateArray().ToList();

        Assert.Equal(2, reminders.Count);
        var byMonths = reminders[0].GetProperty("status");
        Assert.Equal("dueSoon", byMonths.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, byMonths.GetProperty("kmRemaining").ValueKind);
        Assert.Equal(10, byMonths.GetProperty("daysRemaining").GetInt32());
        var both = reminders[1].GetProperty("status");
        Assert.Equal("overdue", both.GetProperty("state").GetString());
        Assert.Equal(5_000, both.GetProperty("kmRemaining").GetInt32());
        Assert.Equal(-9, both.GetProperty("daysRemaining").GetInt32());
    }

    [Fact]
    public async Task Create_trims_the_title()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        await client.CreateReminderAsync(vehicleId, TestData.Reminder(title: "  Troca de óleo  "));

        var reminder = Assert.Single((await client.GetRemindersAsync(vehicleId)).EnumerateArray());
        Assert.Equal("Troca de óleo", reminder.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Create_accepts_the_limits_of_each_field()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var title = new string('a', 120);

        await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            title: title, intervalKm: 1_000_000, intervalMonths: 240, lastDoneOdometer: 2_000_000));
        await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            intervalKm: 1, intervalMonths: 1, lastDoneOdometer: 0));

        var reminders = (await client.GetRemindersAsync(vehicleId)).EnumerateArray().ToList();
        Assert.Equal(title, reminders[0].GetProperty("title").GetString());
        Assert.Equal(1_000_000, reminders[0].GetProperty("intervalKm").GetInt32());
        Assert.Equal(240, reminders[0].GetProperty("intervalMonths").GetInt32());
        Assert.Equal(2_000_000, reminders[0].GetProperty("lastDoneOdometer").GetInt32());
        Assert.Equal(0, reminders[1].GetProperty("lastDoneOdometer").GetInt32());
    }

    [Fact]
    public async Task Create_without_interval_returns_422()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        using var response = await client.PostAsJsonAsync(
            RemindersUrl(vehicleId),
            TestData.Reminder(intervalKm: null, intervalMonths: null, lastDoneOdometer: null),
            TestJson.Options);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "reminder.interval_required");
        Assert.Equal(0, (await client.GetRemindersAsync(vehicleId)).GetArrayLength());
    }

    [Fact]
    public async Task Km_interval_without_base_km_returns_400()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        using var response = await client.PostAsJsonAsync(
            RemindersUrl(vehicleId), TestData.Reminder(lastDoneOdometer: null), TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "lastDoneOdometer" }, FieldNames(errors));
        Assert.Equal("Informe a km da última vez para lembretes por km.", errors.GetProperty("lastDoneOdometer")[0].GetString());
    }

    [Fact]
    public async Task Missing_base_date_returns_400_on_create_and_update()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var id = await client.CreateReminderAsync(vehicleId);
        var rowVersion = (await client.GetReminderAsync(vehicleId, id)).GetProperty("rowVersion").GetUInt32();
        // Sem o campo, o JSON deixaria a data em 0001-01-01 e o lembrete nasceria vencido há séculos.
        var create = TestData.Reminder();
        create.Remove("lastDoneDate");
        var update = TestData.Reminder(title: "Outro").With("rowVersion", rowVersion);
        update.Remove("lastDoneDate");
        var defaultDate = TestData.Reminder().With("lastDoneDate", "0001-01-01");

        using var created = await client.PostAsJsonAsync(RemindersUrl(vehicleId), create, TestJson.Options);
        using var updated = await client.PutAsJsonAsync($"{RemindersUrl(vehicleId)}/{id}", update, TestJson.Options);
        using var explicitDefault = await client.PostAsJsonAsync(RemindersUrl(vehicleId), defaultDate, TestJson.Options);

        foreach (var response in new[] { created, updated, explicitDefault })
        {
            var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
            Assert.Equal(new[] { "lastDoneDate" }, FieldNames(errors));
            Assert.Equal("Informe a data da última vez.", errors.GetProperty("lastDoneDate")[0].GetString());
        }

        var untouched = Assert.Single((await client.GetRemindersAsync(vehicleId)).EnumerateArray());
        Assert.Equal("Troca de óleo", untouched.GetProperty("title").GetString());
        Assert.Equal("2026-09-01", untouched.GetProperty("lastDoneDate").GetString());
    }

    [Fact]
    public async Task Create_with_invalid_title_returns_400()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();

        foreach (var title in new[] { "", "   ", new string('a', 121) })
        {
            using var response = await client.PostAsJsonAsync(
                RemindersUrl(vehicleId), TestData.Reminder(title: title), TestJson.Options);

            var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
            Assert.Equal(new[] { "title" }, FieldNames(errors));
        }
    }

    [Theory]
    [InlineData("intervalKm", 0)]
    [InlineData("intervalKm", 1_000_001)]
    [InlineData("intervalMonths", 0)]
    [InlineData("intervalMonths", 241)]
    [InlineData("lastDoneOdometer", -1)]
    [InlineData("lastDoneOdometer", 2_000_001)]
    public async Task Create_with_out_of_range_numbers_returns_400(string field, int value)
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var body = TestData.Reminder(intervalKm: 5_000, intervalMonths: 12).With(field, value);

        using var response = await client.PostAsJsonAsync(RemindersUrl(vehicleId), body, TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { field }, FieldNames(errors));
    }

    [Fact]
    public async Task Base_date_in_future_returns_422()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 80_000));
        var tomorrow = TestData.Reminder(lastDoneDate: new DateOnly(2026, 10, 11), lastDoneOdometer: 82_000);

        using var rejected = await client.PostAsJsonAsync(RemindersUrl(vehicleId), tomorrow, TestJson.Options);

        await rejected.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "reminder.base_date_in_future");
        // A leitura de km só vale se o lembrete for criado (M6).
        Assert.Equal(80_000, (await client.GetVehicleAsync(vehicleId)).GetProperty("currentOdometer").GetInt32());

        // Hoje ainda é aceito (M5: LastDoneDate <= hoje).
        await client.CreateReminderAsync(vehicleId, TestData.Reminder(lastDoneDate: new DateOnly(2026, 10, 10)));
    }

    [Fact]
    public async Task Base_km_above_current_updates_vehicle_odometer()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 80_000));

        await client.CreateReminderAsync(vehicleId, TestData.Reminder(lastDoneOdometer: 82_000));

        Assert.Equal(82_000, (await client.GetVehicleAsync(vehicleId)).GetProperty("currentOdometer").GetInt32());
    }

    [Fact]
    public async Task Base_km_below_current_keeps_vehicle_odometer()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 90_000));

        await client.CreateReminderAsync(vehicleId, TestData.Reminder(lastDoneOdometer: 80_000));

        Assert.Equal(90_000, (await client.GetVehicleAsync(vehicleId)).GetProperty("currentOdometer").GetInt32());
    }

    [Fact]
    public async Task Update_changes_fields_moves_the_base_back_and_registers_the_base_km()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 84_500));
        var id = await client.CreateReminderAsync(vehicleId);
        var before = (await client.GetReminderAsync(vehicleId, id)).GetProperty("rowVersion").GetUInt32();

        // A base volta de 2026-09-01 para 2026-04-20 e passa a ter só o eixo de meses, com uma leitura de km maior que a do veículo.
        var body = TestData.Reminder(
                title: " Troca do filtro ", intervalKm: null, intervalMonths: 6,
                lastDoneDate: new DateOnly(2026, 4, 20), lastDoneOdometer: 90_000)
            .With("rowVersion", before);
        using var response = await client.PutAsJsonAsync($"{RemindersUrl(vehicleId)}/{id}", body, TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reminder = await client.GetReminderAsync(vehicleId, id);
        Assert.Equal("Troca do filtro", reminder.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, reminder.GetProperty("intervalKm").ValueKind);
        Assert.Equal(6, reminder.GetProperty("intervalMonths").GetInt32());
        Assert.Equal("2026-04-20", reminder.GetProperty("lastDoneDate").GetString());
        Assert.Equal(90_000, reminder.GetProperty("lastDoneOdometer").GetInt32());
        Assert.NotEqual(before, reminder.GetProperty("rowVersion").GetUInt32());
        var status = reminder.GetProperty("status");
        Assert.Equal("dueSoon", status.GetProperty("state").GetString());
        Assert.Equal(10, status.GetProperty("daysRemaining").GetInt32());
        Assert.Equal(90_000, (await client.GetVehicleAsync(vehicleId)).GetProperty("currentOdometer").GetInt32());
    }

    [Fact]
    public async Task Update_enforces_validation_and_domain_rules()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 84_500));
        var id = await client.CreateReminderAsync(vehicleId);
        var url = $"{RemindersUrl(vehicleId)}/{id}";
        var rowVersion = (await client.GetReminderAsync(vehicleId, id)).GetProperty("rowVersion").GetUInt32();

        using var invalid = await client.PutAsJsonAsync(
            url, TestData.Reminder(title: "", lastDoneOdometer: null).With("rowVersion", rowVersion), TestJson.Options);
        using var noInterval = await client.PutAsJsonAsync(
            url,
            TestData.Reminder(title: "Outro", intervalKm: null, intervalMonths: null, lastDoneOdometer: null).With("rowVersion", rowVersion),
            TestJson.Options);
        using var future = await client.PutAsJsonAsync(
            url,
            TestData.Reminder(title: "Outro", lastDoneDate: new DateOnly(2026, 10, 11), lastDoneOdometer: 90_000).With("rowVersion", rowVersion),
            TestJson.Options);

        var errors = (await invalid.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "lastDoneOdometer", "title" }, FieldNames(errors));
        await noInterval.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "reminder.interval_required");
        await future.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "reminder.base_date_in_future");
        var untouched = await client.GetReminderAsync(vehicleId, id);
        Assert.Equal("Troca de óleo", untouched.GetProperty("title").GetString());
        Assert.Equal(rowVersion, untouched.GetProperty("rowVersion").GetUInt32());
        Assert.Equal(84_500, (await client.GetVehicleAsync(vehicleId)).GetProperty("currentOdometer").GetInt32());
    }

    [Fact]
    public async Task Update_with_stale_row_version_returns_409()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var id = await client.CreateReminderAsync(vehicleId);
        var url = $"{RemindersUrl(vehicleId)}/{id}";
        var stale = (await client.GetReminderAsync(vehicleId, id)).GetProperty("rowVersion").GetUInt32();

        // O primeiro PUT muda um campo de verdade; sem alteração o EF nem emite UPDATE e a versão não avança.
        using var first = await client.PutAsJsonAsync(url, TestData.Reminder(title: "Primeiro").With("rowVersion", stale), TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        var current = (await client.GetReminderAsync(vehicleId, id)).GetProperty("rowVersion").GetUInt32();
        Assert.NotEqual(stale, current);

        using var second = await client.PutAsJsonAsync(url, TestData.Reminder(title: "Segundo").With("rowVersion", stale), TestJson.Options);

        await second.AssertProblemAsync(HttpStatusCode.Conflict, "concurrency.conflict");
        Assert.Equal("Primeiro", (await client.GetReminderAsync(vehicleId, id)).GetProperty("title").GetString());

        // Com a versão atual o mesmo PUT passa.
        using var retry = await client.PutAsJsonAsync(url, TestData.Reminder(title: "Segundo").With("rowVersion", current), TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.Equal("Segundo", (await client.GetReminderAsync(vehicleId, id)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Delete_removes_the_reminder()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var kept = await client.CreateReminderAsync(vehicleId, TestData.Reminder(title: "Fica"));
        var removed = await client.CreateReminderAsync(vehicleId, TestData.Reminder(title: "Sai"));
        var url = $"{RemindersUrl(vehicleId)}/{removed}";

        using var deleted = await client.DeleteAsync(url);
        using var deletedAgain = await client.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await deletedAgain.AssertProblemAsync(HttpStatusCode.NotFound, "reminder.not_found");
        var remaining = Assert.Single((await client.GetRemindersAsync(vehicleId)).EnumerateArray());
        Assert.Equal(kept, remaining.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Deleting_the_vehicle_deletes_its_reminders()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        await client.CreateReminderAsync(vehicleId);
        Assert.True(await AnyReminderAsync(vehicleId));

        using var deleted = await client.DeleteAsync($"/api/vehicles/{vehicleId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(await AnyReminderAsync(vehicleId));
    }

    [Fact]
    public async Task Other_user_gets_404()
    {
        factory.Clock.SetUtcNow(Now);
        using var owner = await factory.CreateUserClientAsync();
        using var other = await factory.CreateUserClientAsync();
        var vehicleId = await owner.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 90_000));
        var id = await owner.CreateReminderAsync(vehicleId, TestData.Reminder(lastDoneOdometer: 80_000));
        var rowVersion = (await owner.GetReminderAsync(vehicleId, id)).GetProperty("rowVersion").GetUInt32();
        var url = RemindersUrl(vehicleId);

        using var list = await other.GetAsync(url);
        using var post = await other.PostAsJsonAsync(url, TestData.Reminder(), TestJson.Options);
        using var put = await other.PutAsJsonAsync($"{url}/{id}", TestData.Reminder(title: "Roubado").With("rowVersion", rowVersion), TestJson.Options);
        using var delete = await other.DeleteAsync($"{url}/{id}");

        await list.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await post.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await put.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await delete.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        var untouched = await owner.GetReminderAsync(vehicleId, id);
        Assert.Equal("Troca de óleo", untouched.GetProperty("title").GetString());
        Assert.Equal(rowVersion, untouched.GetProperty("rowVersion").GetUInt32());
        Assert.Equal(1, (await owner.GetRemindersAsync(vehicleId)).GetArrayLength());
        Assert.Equal(0, (await DueAsync(other)).GetArrayLength());
    }

    [Fact]
    public async Task Reminder_of_another_vehicle_or_unknown_gets_404()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleA = await client.CreateVehicleAsync();
        var vehicleB = await client.CreateVehicleAsync();
        var id = await client.CreateReminderAsync(vehicleA);
        var rowVersion = (await client.GetReminderAsync(vehicleA, id)).GetProperty("rowVersion").GetUInt32();

        // Os filhos são sempre buscados por veículo e id: o lembrete de A não é alcançável pelo veículo B.
        using var putThroughB = await client.PutAsJsonAsync(
            $"{RemindersUrl(vehicleB)}/{id}", TestData.Reminder(title: "Trocado").With("rowVersion", rowVersion), TestJson.Options);
        using var deleteThroughB = await client.DeleteAsync($"{RemindersUrl(vehicleB)}/{id}");
        using var putUnknown = await client.PutAsJsonAsync(
            $"{RemindersUrl(vehicleA)}/{Guid.NewGuid()}", TestData.Reminder().With("rowVersion", rowVersion), TestJson.Options);
        using var deleteUnknown = await client.DeleteAsync($"{RemindersUrl(vehicleA)}/{Guid.NewGuid()}");

        await putThroughB.AssertProblemAsync(HttpStatusCode.NotFound, "reminder.not_found");
        await deleteThroughB.AssertProblemAsync(HttpStatusCode.NotFound, "reminder.not_found");
        await putUnknown.AssertProblemAsync(HttpStatusCode.NotFound, "reminder.not_found");
        await deleteUnknown.AssertProblemAsync(HttpStatusCode.NotFound, "reminder.not_found");
        var untouched = await client.GetReminderAsync(vehicleA, id);
        Assert.Equal("Troca de óleo", untouched.GetProperty("title").GetString());
        Assert.Equal(0, (await client.GetRemindersAsync(vehicleB)).GetArrayLength());
    }

    [Fact]
    public async Task Due_lists_overdue_first_and_only_my_vehicles()
    {
        factory.Clock.SetUtcNow(Now);
        using var mine = await factory.CreateUserClientAsync();
        using var theirs = await factory.CreateUserClientAsync();
        var civic = await mine.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 100_000));
        var ek = await mine.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 100_000).With("nickname", "Meu EK"));
        var theirVehicle = await theirs.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 100_000));

        // Em dia: 2026-09-01 + 12 meses está a quase um ano.
        await mine.CreateReminderAsync(civic, TestData.Reminder(
            title: "Em dia", intervalKm: null, intervalMonths: 12, lastDoneOdometer: null));
        // Próximo: 2025-10-20 + 12 meses = 2026-10-20, daqui a 10 dias.
        var dueSoon = await mine.CreateReminderAsync(civic, TestData.Reminder(
            title: "Próximo", intervalKm: null, intervalMonths: 12, lastDoneDate: new DateOnly(2025, 10, 20), lastDoneOdometer: null));
        // Vencido por km: 90.000 + 5.000 - 100.000 = -5.000. Vem antes mesmo sem prazo em dias.
        var overdue = await mine.CreateReminderAsync(ek, TestData.Reminder(
            title: "Vencido", intervalKm: 5_000, lastDoneOdometer: 90_000));
        await theirs.CreateReminderAsync(theirVehicle, TestData.Reminder(
            title: "De outro usuário", intervalKm: 5_000, lastDoneOdometer: 90_000));

        var due = (await DueAsync(mine)).EnumerateArray().ToList();

        Assert.Equal(2, due.Count);
        Assert.Equal(overdue, due[0].GetProperty("reminderId").GetGuid());
        Assert.Equal(ek, due[0].GetProperty("vehicleId").GetGuid());
        Assert.Equal("Meu EK", due[0].GetProperty("vehicleName").GetString());
        Assert.Equal("Vencido", due[0].GetProperty("title").GetString());
        var overdueStatus = due[0].GetProperty("status");
        Assert.Equal("overdue", overdueStatus.GetProperty("state").GetString());
        Assert.Equal(-5_000, overdueStatus.GetProperty("kmRemaining").GetInt32());
        Assert.Equal(JsonValueKind.Null, overdueStatus.GetProperty("daysRemaining").ValueKind);
        Assert.Equal(Now, overdueStatus.GetProperty("odometerUpdatedAt").GetDateTimeOffset());
        Assert.Equal(dueSoon, due[1].GetProperty("reminderId").GetGuid());
        Assert.Equal(civic, due[1].GetProperty("vehicleId").GetGuid());
        Assert.Equal("Honda Civic 1998", due[1].GetProperty("vehicleName").GetString());
        var dueSoonStatus = due[1].GetProperty("status");
        Assert.Equal("dueSoon", dueSoonStatus.GetProperty("state").GetString());
        Assert.Equal(10, dueSoonStatus.GetProperty("daysRemaining").GetInt32());
        Assert.Equal(JsonValueKind.Null, dueSoonStatus.GetProperty("kmRemaining").ValueKind);
    }

    [Fact]
    public async Task Due_orders_overdue_first_then_by_days_then_by_km_remaining()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync(TestData.Vehicle(currentOdometer: 84_800));
        var tenDaysBase = new DateOnly(2025, 10, 20); // + 12 meses: 10 dias
        var twentyDaysBase = new DateOnly(2026, 4, 30); // + 6 meses: 20 dias

        // Criados fora da ordem esperada, para o desempate não depender do id.
        var tenDays800Km = await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            title: "10 dias, 800 km", intervalKm: 5_000, intervalMonths: 12, lastDoneDate: tenDaysBase, lastDoneOdometer: 80_600));
        var twentyDays = await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            title: "20 dias", intervalKm: null, intervalMonths: 6, lastDoneDate: twentyDaysBase, lastDoneOdometer: null));
        var km300 = await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            title: "300 km", intervalKm: 5_000, lastDoneOdometer: 80_100));
        var overdueByKm = await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            title: "Vencido por km", intervalKm: 5_000, lastDoneOdometer: 70_000));
        var tenDays200Km = await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            title: "10 dias, 200 km", intervalKm: 5_000, intervalMonths: 12, lastDoneDate: tenDaysBase, lastDoneOdometer: 80_000));
        var km100 = await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            title: "100 km", intervalKm: 5_000, lastDoneOdometer: 79_900));
        var overdueByDays = await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            title: "Vencido há 9 dias", intervalKm: null, intervalMonths: 6, lastDoneDate: new DateOnly(2026, 4, 1), lastDoneOdometer: null));

        var due = (await DueAsync(client)).EnumerateArray().Select(item => item.GetProperty("reminderId").GetGuid()).ToList();

        Assert.Equal(
            new[] { overdueByDays, overdueByKm, tenDays200Km, tenDays800Km, twentyDays, km100, km300 },
            due);
    }

    [Fact]
    public async Task Due_is_empty_without_reminders_or_when_all_are_ok()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        Assert.Equal(0, (await DueAsync(client)).GetArrayLength());

        await client.CreateReminderAsync(vehicleId, TestData.Reminder(
            intervalKm: null, intervalMonths: 12, lastDoneOdometer: null));

        Assert.Equal(0, (await DueAsync(client)).GetArrayLength());
    }

    [Fact]
    public async Task Repository_GetMany_returns_only_reminders_of_the_given_vehicle()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        var vehicleA = await client.CreateVehicleAsync();
        var vehicleB = await client.CreateVehicleAsync();
        var requestedOfA = await client.CreateReminderAsync(vehicleA);
        await client.CreateReminderAsync(vehicleA);
        var ofB = await client.CreateReminderAsync(vehicleB);

        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReminderRepository>();

        // Só vêm os pedidos que são do veículo: o de B e o id inexistente ficam de fora, e o outro de A não foi pedido.
        var found = await repository.GetManyAsync(vehicleA, [requestedOfA, ofB, Guid.NewGuid()], CancellationToken.None);
        var none = await repository.GetManyAsync(vehicleA, [], CancellationToken.None);

        Assert.Equal(new[] { requestedOfA }, found.Select(reminder => reminder.Id));
        Assert.Empty(none);
    }

    private static string RemindersUrl(Guid vehicleId) => $"/api/vehicles/{vehicleId}/reminders";

    private static async Task<JsonElement> DueAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/reminders/due", TestJson.Options);

    private async Task<bool> AnyReminderAsync(Guid vehicleId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IReadDbContext>()
            .Query<Reminder>()
            .AnyAsync(reminder => reminder.VehicleId == vehicleId);
    }

    private static string[] FieldNames(JsonElement errors) =>
        errors.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
}
