using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Serilog.Core;
using Serilog.Events;

namespace Garage.Api.IntegrationTests.Sharing;

[Collection("api")]
public sealed class PublicViewTests(GarageApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    // Do corpo padrão de TestData.Vehicle.
    private const string Vin = "1HGEJ6678WL000001";
    private const string Notes = "segredo-notas";

    private const string ReminderTitle = "Lembrete secreto";

    [Fact]
    public async Task Public_view_exposes_only_allowlisted_fields()
    {
        var email = $"dono-{Guid.NewGuid():N}@example.com";
        using var owner = await UserAsync(email);
        var seeded = await SeedAsync(owner);
        var link = await owner.CreateShareLinkAsync(
            seeded.VehicleId, new { label = "Comprador", includeCosts = true, includePlate = true });
        var token = Token(link);

        var (view, raw) = await PublicViewAsync(token);

        // Nada fora da allowlist (spec 4.8) aparece em lugar nenhum da resposta: nem dados do dono, nem ids internos.
        string[] forbidden =
        [
            Vin, Notes, "reminder", ReminderTitle, email, "Comprador",
            seeded.VehicleId.ToString(), seeded.RecordId.ToString(), Id(link).ToString(),
            seeded.PrivatePhoto.ToString(), seeded.Document.ToString(),
            "publica.png", "privada.png", "nota-fiscal.pdf"
        ];
        foreach (var value in forbidden)
            Assert.DoesNotContain(value, raw, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            Sorted("make", "model", "version", "generation", "manufactureYear", "modelYear", "fuel", "nickname", "plate",
                "currentOdometer", "includesCosts", "records", "nextCursor"),
            PropertyNames(view));
        Assert.Equal("Honda", view.GetProperty("make").GetString());
        Assert.Equal("Civic", view.GetProperty("model").GetString());
        Assert.Equal("Sedan LX 1.6 16V Mec. 4p", view.GetProperty("version").GetString());
        Assert.Equal("EK", view.GetProperty("generation").GetString());
        Assert.Equal(1997, view.GetProperty("manufactureYear").GetInt32());
        Assert.Equal(1998, view.GetProperty("modelYear").GetInt32());
        Assert.Equal("gasoline", view.GetProperty("fuel").GetString());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("nickname").ValueKind);
        Assert.Equal("ABC1234", view.GetProperty("plate").GetString());
        Assert.Equal(150_000, view.GetProperty("currentOdometer").GetInt32());
        Assert.True(view.GetProperty("includesCosts").GetBoolean());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("nextCursor").ValueKind);

        var record = Assert.Single(view.GetProperty("records").EnumerateArray());
        Assert.Equal(
            Sorted("type", "date", "odometer", "category", "title", "description", "performedBy", "replacedOriginalPart",
                "parts", "laborCost", "totalCost", "photos"),
            PropertyNames(record));
        Assert.Equal("maintenance", record.GetProperty("type").GetString());
        Assert.Equal("2026-10-01", record.GetProperty("date").GetString());
        Assert.Equal(85_000, record.GetProperty("odometer").GetInt32());
        Assert.Equal("engine", record.GetProperty("category").GetString());
        Assert.Equal("Troca de óleo", record.GetProperty("title").GetString());
        Assert.Equal("Óleo 5W30 sintético", record.GetProperty("description").GetString());
        Assert.Equal("Oficina do Zé", record.GetProperty("performedBy").GetString());
        Assert.Equal(JsonValueKind.Null, record.GetProperty("replacedOriginalPart").ValueKind);

        var part = Assert.Single(record.GetProperty("parts").EnumerateArray());
        Assert.Equal(Sorted("name", "brand", "partNumber", "quantity", "unit", "unitPrice"), PropertyNames(part));
        Assert.Equal("Filtro de óleo", part.GetProperty("name").GetString());
        Assert.Equal("Mann", part.GetProperty("brand").GetString());
        Assert.Equal("W 610/3", part.GetProperty("partNumber").GetString());
        Assert.Equal(2m, part.GetProperty("quantity").GetDecimal());
        Assert.Equal("unit", part.GetProperty("unit").GetString());

        // Só a foto pública: nem a foto privada nem o documento.
        var photo = Assert.Single(record.GetProperty("photos").EnumerateArray());
        Assert.Equal(Sorted("id", "path"), PropertyNames(photo));
        Assert.Equal(seeded.PublicPhoto, Id(photo));
        Assert.Equal(PhotoUrl(token, seeded.PublicPhoto), photo.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Costs_appear_only_with_include_costs()
    {
        using var owner = await UserAsync();
        var seeded = await SeedAsync(owner);
        var withoutCosts = Token(await owner.CreateShareLinkAsync(seeded.VehicleId, new { includeCosts = false }));
        var withCosts = Token(await owner.CreateShareLinkAsync(seeded.VehicleId, new { includeCosts = true }));

        var (hiddenView, hiddenRaw) = await PublicViewAsync(withoutCosts);
        var (shownView, _) = await PublicViewAsync(withCosts);

        Assert.False(hiddenView.GetProperty("includesCosts").GetBoolean());
        var hidden = Assert.Single(hiddenView.GetProperty("records").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("laborCost").ValueKind);
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("totalCost").ValueKind);
        var hiddenPart = Assert.Single(hidden.GetProperty("parts").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, hiddenPart.GetProperty("unitPrice").ValueKind);
        // Quantidade não é custo: continua visível.
        Assert.Equal(2m, hiddenPart.GetProperty("quantity").GetDecimal());
        Assert.DoesNotContain("45.9", hiddenRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("171.8", hiddenRaw, StringComparison.Ordinal);

        // 2 × 45,90 + 80,00 de mão de obra = 171,80.
        Assert.True(shownView.GetProperty("includesCosts").GetBoolean());
        var shown = Assert.Single(shownView.GetProperty("records").EnumerateArray());
        Assert.Equal(45.9m, Assert.Single(shown.GetProperty("parts").EnumerateArray()).GetProperty("unitPrice").GetDecimal());
        Assert.Equal(80m, shown.GetProperty("laborCost").GetDecimal());
        Assert.Equal(171.8m, shown.GetProperty("totalCost").GetDecimal());
    }

    [Fact]
    public async Task Plate_appears_only_with_include_plate()
    {
        using var owner = await UserAsync();
        var vehicleId = await owner.CreateVehicleAsync();
        var withoutPlate = Token(await owner.CreateShareLinkAsync(vehicleId, new { includePlate = false }));
        var withPlate = Token(await owner.CreateShareLinkAsync(vehicleId, new { includePlate = true }));

        var (hiddenView, hiddenRaw) = await PublicViewAsync(withoutPlate);
        var (shownView, _) = await PublicViewAsync(withPlate);

        Assert.Equal(JsonValueKind.Null, hiddenView.GetProperty("plate").ValueKind);
        Assert.DoesNotContain("ABC1234", hiddenRaw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ABC-1234", hiddenRaw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ABC1234", shownView.GetProperty("plate").GetString());
    }

    [Fact]
    public async Task Revoked_link_returns_404()
    {
        using var owner = await UserAsync();
        var seeded = await SeedAsync(owner);
        var link = await owner.CreateShareLinkAsync(seeded.VehicleId);
        var token = Token(link);
        await PublicViewAsync(token);

        using (var revoke = await owner.DeleteAsync($"/api/vehicles/{seeded.VehicleId}/share-links/{Id(link)}"))
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var visitor = factory.CreateClient();
        using var view = await visitor.GetAsync(PublicUrl(token));
        using var photo = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));

        await view.AssertProblemAsync(HttpStatusCode.NotFound, "share_link.not_found");
        await photo.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
    }

    [Fact]
    public async Task Expired_link_returns_404()
    {
        var clock = new FakeTimeProvider(Now);
        using var timed = factory.WithClock(clock);
        using var owner = await timed.CreateUserClientAsync();
        var seeded = await SeedAsync(owner);
        var expiresAt = Now.AddDays(7);
        var token = Token(await owner.CreateShareLinkAsync(seeded.VehicleId, new { expiresAt }));
        using var visitor = timed.CreateClient();

        // Um segundo antes da validade, ainda vale.
        clock.SetUtcNow(expiresAt.AddSeconds(-1));
        await PublicViewAsync(token, host: timed);
        using (var validPhoto = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto)))
            Assert.Equal(HttpStatusCode.OK, validPhoto.StatusCode);

        clock.SetUtcNow(expiresAt.AddDays(1));
        using var view = await visitor.GetAsync(PublicUrl(token));
        using var photo = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));

        await view.AssertProblemAsync(HttpStatusCode.NotFound, "share_link.not_found");
        await photo.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        var link = Assert.Single((await owner.GetShareLinksAsync(seeded.VehicleId)).EnumerateArray());
        Assert.False(link.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Malformed_token_returns_404()
    {
        factory.Clock.SetUtcNow(Now);
        using var visitor = factory.CreateClient();
        string[] malformed =
        [
            "x",
            new string('a', 42) + "!",
            new string('a', 42) + "+",
            new string('a', 44),
            new string('a', 200)
        ];

        // Bem formado mas inexistente: a resposta é a mesma de um token malformado (nada distingue os casos).
        using var unknown = await visitor.GetAsync(PublicUrl(new string('A', 43)));
        var expected = await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "share_link.not_found");

        foreach (var token in malformed)
        {
            using var view = await visitor.GetAsync(PublicUrl(token));
            using var photo = await visitor.GetAsync(PhotoUrl(token, Guid.NewGuid()));

            var problem = await view.AssertProblemAsync(HttpStatusCode.NotFound, "share_link.not_found");
            Assert.Equal(expected.GetProperty("detail").GetString(), problem.GetProperty("detail").GetString());
            await photo.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        }
    }

    [Fact]
    public async Task Public_photo_downloads_and_private_or_document_returns_404()
    {
        using var owner = await UserAsync();
        var seeded = await SeedAsync(owner);
        var otherVehicle = await owner.CreateVehicleAsync();
        var otherRecord = await owner.CreateRecordAsync(otherVehicle);
        var otherPhoto = Id(await owner.UploadAttachmentAsync(otherVehicle, otherRecord, TestImages.TinyPng(), "outra.png"));
        var token = Token(await owner.CreateShareLinkAsync(seeded.VehicleId));
        using var visitor = factory.CreateClient();

        using var photo = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));

        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
        var disposition = photo.Content.Headers.ContentDisposition;
        Assert.Equal("inline", disposition?.DispositionType);
        // O nome original do arquivo é do dono: o visitante recebe só o id do anexo.
        Assert.Equal($"{seeded.PublicPhoto}.png", disposition?.FileName?.Trim('"'));
        Assert.Equal(
            await OwnerDownloadAsync(owner, seeded.VehicleId, seeded.PublicPhoto),
            await photo.Content.ReadAsByteArrayAsync());

        // Foto privada, documento, foto pública de outro veículo do mesmo dono e id desconhecido: todos 404.
        foreach (var id in new[] { seeded.PrivatePhoto, seeded.Document, otherPhoto, Guid.NewGuid() })
        {
            using var response = await visitor.GetAsync(PhotoUrl(token, id));
            await response.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        }

        // Ocultar a foto vale na hora, também para links já criados.
        using (var hide = await owner.PatchAsJsonAsync(
                   $"/api/vehicles/{seeded.VehicleId}/attachments/{seeded.PublicPhoto}", new { isPublic = false }, TestJson.Options))
            Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);
        using var hidden = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));
        await hidden.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
    }

    [Fact]
    public async Task Public_responses_have_noindex_and_no_referrer_even_on_404()
    {
        using var owner = await UserAsync();
        var seeded = await SeedAsync(owner);
        var token = Token(await owner.CreateShareLinkAsync(seeded.VehicleId));
        using var visitor = factory.CreateClient();

        using var view = await visitor.GetAsync(PublicUrl(token));
        using var photo = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));
        using var unknownLink = await visitor.GetAsync(PublicUrl(new string('A', 43)));
        using var malformed = await visitor.GetAsync(PublicUrl("x"));
        using var privatePhoto = await visitor.GetAsync(PhotoUrl(token, seeded.PrivatePhoto));
        using var invalidCursor = await visitor.GetAsync($"{PublicUrl(token)}?cursor=invalido");

        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownLink.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, privatePhoto.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        foreach (var response in new[] { view, photo, unknownLink, malformed, privatePhoto, invalidCursor })
        {
            Assert.Equal(["noindex"], response.Headers.GetValues("X-Robots-Tag"));
            Assert.Equal(["no-referrer"], response.Headers.GetValues("Referrer-Policy"));
        }
    }

    [Fact]
    public async Task Public_history_is_paginated_30_records_at_a_time()
    {
        using var owner = await UserAsync();
        var vehicleId = await owner.CreateVehicleAsync();
        var first = new DateOnly(2026, 1, 1);
        for (var day = 0; day < 31; day++)
            await owner.CreateRecordAsync(vehicleId, TestData.Record(title: $"Registro {day}", date: first.AddDays(day), odometer: null));
        var token = Token(await owner.CreateShareLinkAsync(vehicleId));

        var (firstPage, _) = await PublicViewAsync(token);
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        Assert.NotNull(cursor);
        var (secondPage, _) = await PublicViewAsync(token, $"?cursor={Uri.EscapeDataString(cursor)}");

        // Mais recentes primeiro: de 31/01 a 02/01 na primeira página e 01/01 na segunda, que é a última.
        var expectedDates = Enumerable.Range(0, 30)
            .Select(i => first.AddDays(30 - i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Assert.Equal(expectedDates, Dates(firstPage));
        Assert.Equal(["2026-01-01"], Dates(secondPage));
        Assert.Equal(JsonValueKind.Null, secondPage.GetProperty("nextCursor").ValueKind);
        Assert.Equal("Honda", secondPage.GetProperty("make").GetString());

        using var visitor = factory.CreateClient();
        using var invalid = await visitor.GetAsync($"{PublicUrl(token)}?cursor=invalido");
        var errors = (await invalid.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(["cursor"], PropertyNames(errors));
    }

    [Fact]
    public async Task Public_view_and_photos_have_separate_rate_limits()
    {
        using var owner = await UserAsync();
        var seeded = await SeedAsync(owner);
        var token = Token(await owner.CreateShareLinkAsync(seeded.VehicleId));
        using var limited = factory.WithWebHostBuilder(builder => builder
            .UseSetting("RateLimits:PublicPerMinute", "2")
            .UseSetting("RateLimits:PublicPhotosPerMinute", "3"));
        using var visitor = limited.CreateClient();

        // A visão JSON esgota a cota da política "public"...
        using var firstView = await visitor.GetAsync(PublicUrl(token));
        using var secondView = await visitor.GetAsync(PublicUrl(token));
        using var thirdView = await visitor.GetAsync(PublicUrl(token));
        // ...e as fotos da mesma página seguem carregando: a cota delas é outra ("public-photos"), por IP também.
        var photos = new List<HttpStatusCode>();
        for (var photo = 0; photo < 3; photo++)
        {
            using var response = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));
            photos.Add(response.StatusCode);
        }

        using var overPhotoLimit = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));

        Assert.Equal(HttpStatusCode.OK, firstView.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondView.StatusCode);
        await thirdView.AssertProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
        Assert.All(photos, status => Assert.Equal(HttpStatusCode.OK, status));
        await overPhotoLimit.AssertProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
    }

    [Fact]
    public async Task Public_view_is_never_cached_and_photos_are_cached_privately_for_an_hour()
    {
        using var owner = await UserAsync();
        var seeded = await SeedAsync(owner);
        var token = Token(await owner.CreateShareLinkAsync(seeded.VehicleId));
        using var visitor = factory.CreateClient();

        using var view = await visitor.GetAsync(PublicUrl(token));
        using var photo = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));
        using var unknownLink = await visitor.GetAsync(PublicUrl(new string('A', 43)));
        using var privatePhoto = await visitor.GetAsync(PhotoUrl(token, seeded.PrivatePhoto));

        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownLink.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, privatePhoto.StatusCode);
        // A visão muda a cada edição do dono e some quando o link é revogado: nunca fica em cache.
        foreach (var response in new[] { view, unknownLink, privatePhoto })
            Assert.True(response.Headers.CacheControl?.NoStore, $"Sem no-store em {response.RequestMessage?.RequestUri}.");
        // A foto pode ficar no cache do navegador de quem já a viu por até 1 h; revogar o link barra as cargas novas.
        var photoCache = photo.Headers.CacheControl;
        Assert.NotNull(photoCache);
        Assert.True(photoCache.Private);
        Assert.False(photoCache.NoStore);
        Assert.Equal(TimeSpan.FromHours(1), photoCache.MaxAge);
    }

    [Fact]
    public async Task Public_token_never_reaches_the_logs()
    {
        using var owner = await UserAsync();
        var seeded = await SeedAsync(owner);
        var token = Token(await owner.CreateShareLinkAsync(seeded.VehicleId));
        var sink = new CapturingLogSink();
        // Um wwwroot mínimo, para a página /p/{token} sair pelo fallback da SPA como em produção.
        var webRoot = Path.Combine(Path.GetTempPath(), "garage-tests", "spa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRoot);
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<!doctype html><div id=\"root\"></div>");
        try
        {
            using var logged = factory.WithWebHostBuilder(builder => builder
                .UseWebRoot(webRoot)
                .ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(sink)));
            using var visitor = logged.CreateClient();

            using var view = await visitor.GetAsync(PublicUrl(token));
            using var photo = await visitor.GetAsync(PhotoUrl(token, seeded.PublicPhoto));
            using var page = await visitor.GetAsync($"/p/{token}");
            using var unknown = await visitor.GetAsync(PhotoUrl(token, Guid.NewGuid()));

            Assert.Equal(HttpStatusCode.OK, view.StatusCode);
            Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            string[] maskedPaths = ["/api/public/***", $"/api/public/***/attachments/{seeded.PublicPhoto}"];
            var allRequestsLogged = await sink.WaitUntilAsync(captured => captured.Count(IsRequestLog) >= 4);
            var events = sink.Events;
            // O token é a credencial do link: não pode aparecer em nenhum evento, nem em propriedade nem na mensagem.
            foreach (var logEvent in events)
            {
                var text = CapturingLogSink.Describe(logEvent);
                Assert.False(text.Contains(token, StringComparison.Ordinal), $"O token apareceu no log:{Environment.NewLine}{text}");
            }

            Assert.True(allRequestsLogged, $"Esperava o log das 4 requisições; vieram {events.Count(IsRequestLog)}.");
            var requestPaths = events.Where(IsRequestLog).Select(e => CapturingLogSink.Scalar(e, "RequestPath")).ToList();
            Assert.All(maskedPaths, path => Assert.Contains(path, requestPaths));
        }
        finally
        {
            try
            {
                Directory.Delete(webRoot, recursive: true);
            }
            catch (IOException)
            {
                // Limpeza de temporário: um arquivo ainda aberto pelo host de teste não deve reprovar o teste.
            }
        }
    }

    /// <summary>
    /// Veículo padrão (com chassi, observações "segredo-notas" e placa) e um registro com peça (2 × 45,90) e mão de obra
    /// (80,00), com uma foto pública, uma foto privada e um documento; mais um lembrete.
    /// </summary>
    private static async Task<Seeded> SeedAsync(HttpClient owner)
    {
        var vehicleId = await owner.CreateVehicleAsync();
        var record = TestData.Record(
                laborCost: 80.00m,
                parts: [TestData.Part(quantity: 2m, unitPrice: 45.90m, brand: "Mann", partNumber: "W 610/3")])
            .With("description", "Óleo 5W30 sintético")
            .With("performedBy", "Oficina do Zé");
        var recordId = await owner.CreateRecordAsync(vehicleId, record);

        var publicPhoto = Id(await owner.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "publica.png"));
        var privatePhoto = Id(await owner.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "privada.png"));
        using (var hide = await owner.PatchAsJsonAsync(
                   $"/api/vehicles/{vehicleId}/attachments/{privatePhoto}", new { isPublic = false }, TestJson.Options))
            Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);
        var document = Id(await owner.UploadAttachmentAsync(
            vehicleId, recordId, TestImages.Pdf(), "nota-fiscal.pdf", kind: "document"));

        await owner.CreateReminderAsync(vehicleId, TestData.Reminder(title: ReminderTitle));
        return new Seeded(vehicleId, recordId, publicPhoto, privatePhoto, document);
    }

    private async Task<HttpClient> UserAsync(string? email = null)
    {
        factory.Clock.SetUtcNow(Now);
        return await factory.CreateUserClientAsync(email);
    }

    /// <summary>A visão pública como um visitante sem sessão (em <paramref name="host"/>, ou na fábrica); deve dar 200.</summary>
    private async Task<(JsonElement View, string Raw)> PublicViewAsync(
        string token, string query = "", WebApplicationFactory<Program>? host = null)
    {
        using var visitor = (host ?? factory).CreateClient();
        using var response = await visitor.GetAsync(PublicUrl(token) + query);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Public view failed: {(int)response.StatusCode} {raw}");

        return (JsonSerializer.Deserialize<JsonElement>(raw, TestJson.Options), raw);
    }

    private static async Task<byte[]> OwnerDownloadAsync(HttpClient owner, Guid vehicleId, Guid id)
    {
        using var response = await owner.GetAsync($"/api/vehicles/{vehicleId}/attachments/{id}/content");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static bool IsRequestLog(LogEvent logEvent) =>
        CapturingLogSink.Scalar(logEvent, "SourceContext") == "Serilog.AspNetCore.RequestLoggingMiddleware";

    private static string PublicUrl(string token) => $"/api/public/{token}";

    private static string PhotoUrl(string token, Guid id) => $"/api/public/{token}/attachments/{id}";

    private static Guid Id(JsonElement element) => element.GetProperty("id").GetGuid();

    private static string Token(JsonElement link) => link.GetProperty("token").GetString()!;

    private static string[] Dates(JsonElement view) =>
        view.GetProperty("records").EnumerateArray().Select(record => record.GetProperty("date").GetString()!).ToArray();

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();

    private static string[] Sorted(params string[] names) => names.Order(StringComparer.Ordinal).ToArray();

    private sealed record Seeded(Guid VehicleId, Guid RecordId, Guid PublicPhoto, Guid PrivatePhoto, Guid Document);
}
