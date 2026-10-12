using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Infrastructure.Persistence;
using Garage.Infrastructure.Storage;
using ImageMagick;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Garage.Api.IntegrationTests.Demo;

/// <summary>As fotos e a nota de exemplo que o <c>POST /api/demo/seed</c> anexa aos registros de demonstração.</summary>
[Collection("api")]
public sealed class DemoSeedAttachmentsTests(GarageApiFactory factory)
{
    private const string SeedUrl = "/api/demo/seed";
    private const string Civic = "Civic EK";
    private const string Gol = "Volkswagen Gol 2019";

    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    // Registro | tipo | visibilidade | nome exibido | tipo do conteúdo gravado.
    private static readonly string[] ExpectedCivic =
    [
        "Troca da correia dentada | photo | public | correia-dentada.jpg | image/jpeg",
        "Pastilhas de freio dianteiras | photo | public | pastilhas-freio.jpg | image/jpeg",
        "Alinhamento e balanceamento | photo | public | alinhamento.jpg | image/jpeg",
        "Coilover | photo | public | coilover.jpg | image/jpeg",
        "Coilover | photo | public | amortecedores-originais.jpg | image/jpeg",
        "Coilover | document | private | coilover-nota-exemplo.pdf | application/pdf",
        "Troca de óleo e filtro | photo | public | oleo.jpg | image/jpeg",
        "Troca de óleo e filtro | photo | public | filtro-oleo.jpg | image/jpeg",
        "Repintura do para-choque dianteiro | photo | public | para-choque.jpg | image/jpeg"
    ];

    private static readonly string[] ExpectedGol =
    [
        "Troca dos quatro pneus | photo | private | pneus.jpg | image/jpeg"
    ];

    // Autor e licença de cada foto (Wikimedia Commons) na descrição do registro: é o que a página pública mostra.
    private static readonly (string Title, string Description)[] ExpectedCivicDescriptions =
    [
        ("Troca da correia dentada",
            "Kit completo com tensor; bomba d'água trocada junto.\nCrédito da foto (Wikimedia Commons): Terabass (CC BY-SA 3.0)."),
        ("Pastilhas de freio dianteiras",
            "Trocadas por desgaste; discos medidos e dentro do limite.\n"
            + "Crédito da foto (Wikimedia Commons): Riley from Christchurch, New Zealand (CC BY 2.0)."),
        ("Alinhamento e balanceamento",
            "Crédito da foto (Wikimedia Commons): Mike Peel (www.mikepeel.net) (CC BY-SA 4.0)."),
        ("Coilover",
            "Kit rosqueável regulável; altura ajustada para o uso diário.\n"
            + "Crédito das fotos (Wikimedia Commons): Fortune Auto North America (CC BY-SA 4.0); Shixart1985 (CC BY 2.0)."),
        ("Troca de óleo e filtro",
            "Óleo 5W30 sintético.\n"
            + "Crédito das fotos (Wikimedia Commons): Santeri Viinamäki (CC BY-SA 4.0); Harrikkamies (domínio público)."),
        ("Repintura do para-choque dianteiro",
            "Retoque de arranhões e repintura na cor original.\nCrédito da foto (Wikimedia Commons): Elise240SX (CC BY-SA 4.0).")
    ];

    private const string ExpectedGolDescription =
        "Montagem e balanceamento incluídos na mão de obra.\nCrédito da foto (Wikimedia Commons): ProjectManhattan (CC BY-SA 3.0).";

    // O maior lado que o reprocessamento deixa (A5); uma das fotos de origem tem 2876 px de altura.
    private const uint MaxSide = 2560;

    // Marcas que só existem em metadados (EXIF, XMP, perfil ICC e a proveniência C2PA/JUMBF): quase todas as fotos de
    // origem trazem pelo menos uma, e o reprocessamento não pode deixar nenhuma nos bytes gravados.
    private static readonly string[] MetadataMarkers = ["Exif", "ns.adobe.com", "ICC_PROFILE", "jumb", "c2pa"];

    [Fact]
    public async Task Demo_seed_attaches_photos_and_a_sample_invoice_to_the_records()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);

        var civic = await ListAttachmentsAsync(client, await FindVehicleIdAsync(client, Civic));
        var gol = await ListAttachmentsAsync(client, await FindVehicleIdAsync(client, Gol));

        // O Civic tem 9 anexos (8 fotos + 1 documento): o "Coilover" 2 fotos e a nota, a troca de óleo 2 fotos; o Gol, 1
        // foto, que não é pública.
        Assert.Equal(9, civic.Count);
        Assert.Equal(8, civic.Count(file => file.Kind == "photo"));
        Assert.Equal(1, civic.Count(file => file.Kind == "document"));
        Assert.Equal(2, civic.Count(file => file.Title == "Coilover" && file.Kind == "photo"));
        Assert.Equal(1, civic.Count(file => file.Title == "Coilover" && file.Kind == "document"));
        Assert.Equal(2, civic.Count(file => file.Title == "Troca de óleo e filtro" && file.Kind == "photo"));
        Assert.Equal(Sorted(ExpectedCivic), Sorted(civic.Select(file => file.Signature)));
        Assert.Equal(Sorted(ExpectedGol), Sorted(gol.Select(file => file.Signature)));

        // Os limites do domínio valem para os arquivos de exemplo (A1 e A3).
        Assert.All(civic.Concat(gol), file => Assert.InRange(file.SizeBytes, 1, Attachment.MaxSizeBytes));
        Assert.All(civic.Concat(gol).GroupBy(file => file.Title), group => Assert.InRange(group.Count(), 1, Attachment.MaxPerRecord));
    }

    [Fact]
    public async Task Demo_seed_credits_the_authors_and_licenses_of_the_photos_in_the_record_descriptions()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);
        var civicId = await FindVehicleIdAsync(client, Civic);
        var golId = await FindVehicleIdAsync(client, Gol);
        var link = Assert.Single((await client.GetShareLinksAsync(civicId)).EnumerateArray());

        // O crédito aparece onde as fotos aparecem: na página pública do Civic...
        using var anonymous = factory.CreateCookieClient();
        var view = await anonymous.GetFromJsonAsync<JsonElement>($"/api/public/{link.GetProperty("token").GetString()}", TestJson.Options);
        var publicDescriptions = view.GetProperty("records").EnumerateArray()
            .ToDictionary(record => record.GetProperty("title").GetString()!, record => record.GetProperty("description").GetString());
        Assert.Equal(ExpectedCivicDescriptions.Length, publicDescriptions.Count);
        foreach (var (title, description) in ExpectedCivicDescriptions)
            Assert.Equal(description, publicDescriptions[title]);

        // ...e para o dono, inclusive na foto do Gol, que não é pública.
        var tires = Assert.Single((await client.GetRecordsAsync(golId)).GetProperty("items").EnumerateArray(),
            item => item.GetProperty("title").GetString() == "Troca dos quatro pneus");
        var tiresDetail = await client.GetRecordAsync(golId, tires.GetProperty("id").GetGuid());
        Assert.Equal(ExpectedGolDescription, tiresDetail.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Demo_seed_public_link_shows_exactly_the_public_civic_photos_as_clean_jpegs()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);
        var civicId = await FindVehicleIdAsync(client, Civic);
        var attachments = await ListAttachmentsAsync(client, civicId);
        var link = Assert.Single((await client.GetShareLinksAsync(civicId)).EnumerateArray());
        var token = link.GetProperty("token").GetString()!;

        using var anonymous = factory.CreateCookieClient();
        using var viewResponse = await anonymous.GetAsync($"/api/public/{token}");
        var body = await viewResponse.Content.ReadAsStringAsync();
        Assert.True(viewResponse.StatusCode == HttpStatusCode.OK, $"Public view failed: {(int)viewResponse.StatusCode} {body}");
        var view = JsonSerializer.Deserialize<JsonElement>(body, TestJson.Options);
        var photos = view.GetProperty("records").EnumerateArray()
            .SelectMany(record => record.GetProperty("photos").EnumerateArray())
            .ToList();

        // Exatamente as 8 fotos públicas do Civic: nenhum documento, e é o mesmo conjunto que o dono vê como público.
        Assert.Equal(8, photos.Count);
        var publicIds = attachments.Where(file => file.IsPublic).Select(file => file.Id).Order();
        Assert.Equal(publicIds, photos.Select(photo => photo.GetProperty("id").GetGuid()).Order());
        var invoice = Assert.Single(attachments, file => file.Kind == "document");
        Assert.DoesNotContain(photos, photo => photo.GetProperty("id").GetGuid() == invoice.Id);

        foreach (var photo in photos)
        {
            using var download = await anonymous.GetAsync(photo.GetProperty("path").GetString());
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("image/jpeg", download.Content.Headers.ContentType?.MediaType);
            AssertCleanJpeg(await download.Content.ReadAsByteArrayAsync());
        }

        // A nota nunca sai pelo link, nem pelo caminho direto.
        using var privateDownload = await anonymous.GetAsync($"/api/public/{token}/attachments/{invoice.Id}");
        Assert.Equal(HttpStatusCode.NotFound, privateDownload.StatusCode);
    }

    [Fact]
    public async Task Demo_seed_owner_downloads_the_sample_invoice_as_a_pdf_and_the_private_photo_as_a_clean_jpeg()
    {
        factory.Clock.SetUtcNow(Now);
        using var client = await factory.CreateUserClientAsync();
        await SeedAsync(client);
        var civicId = await FindVehicleIdAsync(client, Civic);
        var golId = await FindVehicleIdAsync(client, Gol);
        var invoice = Assert.Single(await ListAttachmentsAsync(client, civicId), file => file.Kind == "document");
        var tires = Assert.Single(await ListAttachmentsAsync(client, golId));

        using var pdfResponse = await client.GetAsync(ContentUrl(civicId, invoice.Id));
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", pdfResponse.Content.Headers.ContentDisposition?.DispositionType);
        var pdf = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5), StringComparison.Ordinal);
        Assert.Equal(invoice.SizeBytes, pdf.Length);

        using var photoResponse = await client.GetAsync(ContentUrl(golId, tires.Id));
        Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
        Assert.Equal("image/jpeg", photoResponse.Content.Headers.ContentType?.MediaType);
        AssertCleanJpeg(await photoResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Demo_seed_leaves_no_file_on_disk_when_a_write_fails_halfway()
    {
        factory.Clock.SetUtcNow(Now);
        using var disk = new IsolatedStorage();
        using var failing = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:RootPath", disk.Path);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFileStorage>();
                // O 4º arquivo é gravado por inteiro e só então a gravação "falha": é o pior caso para a limpeza, que
                // precisa ter sido registrada antes de gravar.
                services.AddSingleton<IFileStorage>(sp => new FailingStorage(
                    ActivatorUtilities.CreateInstance<LocalDiskFileStorage>(sp), failOnSave: 4));
            });
        });
        using var client = await failing.CreateUserClientAsync();

        using var response = await client.PostAsync(SeedUrl, content: null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await GetVehiclesAsync(client));
        Assert.Empty(disk.Files());
        Assert.Equal(4, ((FailingStorage)failing.Services.GetRequiredService<IFileStorage>()).Saves);
    }

    [Fact]
    public async Task Demo_seed_leaves_no_file_on_disk_when_the_commit_fails()
    {
        factory.Clock.SetUtcNow(Now);
        using var disk = new IsolatedStorage();
        var probe = new CommitProbe();
        using var failing = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:RootPath", disk.Path);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUnitOfWork>();
                services.AddScoped<UnitOfWork>();
                services.AddScoped<IUnitOfWork>(sp => new FailingCommitUnitOfWork(sp.GetRequiredService<UnitOfWork>(), disk, probe));
            });
        });
        using var client = await failing.CreateUserClientAsync();

        using var response = await client.PostAsync(SeedUrl, content: null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await GetVehiclesAsync(client));
        Assert.Empty(disk.Files());
        // Na hora do commit os 10 arquivos (9 do Civic + 1 do Gol) já estavam no disco: foi a limpeza que os apagou.
        Assert.Equal(10, probe.FilesOnDisk);
    }

    private static string[] Sorted(IEnumerable<string> values) => values.Order(StringComparer.Ordinal).ToArray();

    private static string ContentUrl(Guid vehicleId, Guid id) => $"/api/vehicles/{vehicleId}/attachments/{id}/content";

    private static void AssertCleanJpeg(byte[] bytes)
    {
        using var image = new MagickImage(bytes);
        Assert.Equal(MagickFormat.Jpeg, image.Format);
        Assert.InRange(image.Width, 1u, MaxSide);
        Assert.InRange(image.Height, 1u, MaxSide);
        Assert.Null(image.GetExifProfile());
        Assert.Empty(image.ProfileNames);
        foreach (var marker in MetadataMarkers)
            Assert.False(bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(marker)) >= 0, $"O JPEG gravado ainda contém \"{marker}\".");
    }

    private static async Task<JsonElement> SeedAsync(HttpClient client)
    {
        using var response = await client.PostAsync(SeedUrl, content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Seed failed: {(int)response.StatusCode} {body}");

        return JsonSerializer.Deserialize<JsonElement>(body, TestJson.Options);
    }

    private static async Task<List<JsonElement>> GetVehiclesAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/vehicles");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"List vehicles failed: {(int)response.StatusCode} {content}");

        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options).EnumerateArray().ToList();
    }

    private static async Task<Guid> FindVehicleIdAsync(HttpClient client, string displayName) =>
        Assert.Single(await GetVehiclesAsync(client), vehicle => vehicle.GetProperty("displayName").GetString() == displayName)
            .GetProperty("id").GetGuid();

    /// <summary>Todos os anexos de todos os registros do veículo, lidos pelo detalhe de cada registro.</summary>
    private static async Task<List<Attached>> ListAttachmentsAsync(HttpClient client, Guid vehicleId)
    {
        List<Attached> files = [];
        foreach (var item in (await client.GetRecordsAsync(vehicleId)).GetProperty("items").EnumerateArray())
        {
            var record = await client.GetRecordAsync(vehicleId, item.GetProperty("id").GetGuid());
            var title = record.GetProperty("title").GetString()!;
            foreach (var attachment in record.GetProperty("attachments").EnumerateArray())
            {
                files.Add(new Attached(
                    attachment.GetProperty("id").GetGuid(),
                    title,
                    attachment.GetProperty("kind").GetString()!,
                    attachment.GetProperty("isPublic").GetBoolean(),
                    attachment.GetProperty("fileName").GetString()!,
                    attachment.GetProperty("contentType").GetString()!,
                    attachment.GetProperty("sizeBytes").GetInt64()));
            }
        }

        return files;
    }

    private sealed record Attached(Guid Id, string Title, string Kind, bool IsPublic, string FileName, string ContentType, long SizeBytes)
    {
        public string Signature => $"{Title} | {Kind} | {(IsPublic ? "public" : "private")} | {FileName} | {ContentType}";
    }

    /// <summary>Pasta de anexos só do teste: as fábricas derivadas dividem o <c>Storage:RootPath</c> com os outros testes.</summary>
    private sealed class IsolatedStorage : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "garage-tests", $"demo-{Guid.NewGuid():N}");

        public IsolatedStorage() => Directory.CreateDirectory(Path);

        public string[] Files() => Directory.GetFiles(Path, "*", SearchOption.AllDirectories);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>Grava de verdade e então falha na <paramref name="failOnSave"/>-ésima gravação: o arquivo fica no disco.</summary>
    private sealed class FailingStorage(IFileStorage inner, int failOnSave) : IFileStorage
    {
        private int _saves;

        public int Saves => _saves;

        public async Task SaveAsync(string key, Stream content, CancellationToken ct)
        {
            await inner.SaveAsync(key, content, ct);
            if (Interlocked.Increment(ref _saves) == failOnSave)
                throw new IOException("Disco cheio (simulado).");
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => inner.OpenReadAsync(key, ct);

        public Task DeleteAsync(string key, CancellationToken ct) => inner.DeleteAsync(key, ct);
    }

    /// <summary>Quantos arquivos estavam no disco quando o commit foi pedido (-1 se ele nunca foi pedido).</summary>
    private sealed class CommitProbe
    {
        public int FilesOnDisk { get; set; } = -1;
    }

    /// <summary>
    /// Faz o que o <see cref="UnitOfWork"/> de verdade faz quando o banco recusa o commit: descarta o trabalho (roda as
    /// ações de rollback) e repropaga a exceção. Antes, conta os arquivos que estavam no disco.
    /// </summary>
    private sealed class FailingCommitUnitOfWork(UnitOfWork inner, IsolatedStorage disk, CommitProbe probe) : IUnitOfWork
    {
        public async Task<Result> SaveChangesAsync(CancellationToken ct)
        {
            probe.FilesOnDisk = disk.Files().Length;
            await inner.DiscardAsync(CancellationToken.None);
            throw new DbUpdateException("Commit recusado (simulado).");
        }

        public Task DiscardAsync(CancellationToken ct) => inner.DiscardAsync(ct);

        public void AfterCommit(Func<CancellationToken, Task> action) => inner.AfterCommit(action);

        public void OnRollback(Func<CancellationToken, Task> action) => inner.OnRollback(action);
    }
}
