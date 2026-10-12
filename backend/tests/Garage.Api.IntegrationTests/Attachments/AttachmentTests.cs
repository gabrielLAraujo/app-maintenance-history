using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Domain.Attachments;
using ImageMagick;
using Microsoft.AspNetCore.Hosting;

namespace Garage.Api.IntegrationTests.Attachments;

[Collection("api")]
public sealed class AttachmentTests(GarageApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upload_jpeg_photo_strips_exif_resizes_and_is_public()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var original = TestImages.Jpeg(3000, 2000, withGps: true);
        using (var sent = new MagickImage(original))
            Assert.NotNull(sent.GetExifProfile()?.GetValue(ExifTag.GPSLatitude)); // o arquivo enviado tem GPS

        using var response = await client.PostAttachmentAsync(vehicleId, recordId, original, "oleo.jpg");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attachment = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var id = Id(attachment);
        Assert.Equal(ContentUrl(vehicleId, id), response.Headers.Location?.OriginalString);
        Assert.Equal("photo", attachment.GetProperty("kind").GetString());
        Assert.True(attachment.GetProperty("isPublic").GetBoolean());
        Assert.Equal("oleo.jpg", attachment.GetProperty("fileName").GetString());
        Assert.Equal("image/jpeg", attachment.GetProperty("contentType").GetString());
        Assert.Equal(Now, attachment.GetProperty("uploadedAt").GetDateTimeOffset());

        using var download = await client.GetAsync(ContentUrl(vehicleId, id));

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/jpeg", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", download.Content.Headers.ContentDisposition?.DispositionType);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(attachment.GetProperty("sizeBytes").GetInt64(), bytes.Length);
        using var stored = new MagickImage(bytes);
        Assert.Equal(MagickFormat.Jpeg, stored.Format);
        Assert.Null(stored.GetExifProfile());
        Assert.Empty(stored.ProfileNames);
        Assert.False(ContainsAscii(bytes, "Exif"), "O JPEG gravado ainda tem um bloco EXIF.");
        Assert.Equal(85u, stored.Quality);
        // Só reduz, mantendo a proporção: 3000×2000 → 2560×1707.
        Assert.Equal(2560u, stored.Width);
        Assert.InRange(stored.Height, 1706u, 1707u);
    }

    [Fact]
    public async Task Upload_rotated_jpeg_is_auto_oriented_before_metadata_is_removed()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        // Foto de celular em pé: pixels deitados (3000×2000) e EXIF Orientation 6 (girar 90° para exibir).
        var rotated = TestImages.Jpeg(3000, 2000, withGps: false, orientation: 6);

        var attachment = await client.UploadAttachmentAsync(vehicleId, recordId, rotated, "em-pe.jpg");

        using var stored = new MagickImage(await DownloadAsync(client, vehicleId, Id(attachment)));
        Assert.Null(stored.GetExifProfile());
        Assert.Equal(2560u, stored.Height);
        Assert.InRange(stored.Width, 1706u, 1707u);
    }

    [Fact]
    public async Task Upload_png_stays_png_and_webp_becomes_jpeg_without_upscaling()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);

        var png = await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "peca.png");
        var webp = await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.Webp(), "painel.webp");

        Assert.Equal("image/png", png.GetProperty("contentType").GetString());
        using (var storedPng = new MagickImage(await DownloadAsync(client, vehicleId, Id(png))))
        {
            Assert.Equal(MagickFormat.Png, storedPng.Format);
            Assert.Equal((2u, 2u), (storedPng.Width, storedPng.Height));
        }

        Assert.Equal("image/jpeg", webp.GetProperty("contentType").GetString());
        using var storedWebp = new MagickImage(await DownloadAsync(client, vehicleId, Id(webp)));
        Assert.Equal(MagickFormat.Jpeg, storedWebp.Format);
        Assert.Equal((40u, 30u), (storedWebp.Width, storedWebp.Height));
    }

    [Fact]
    public async Task Upload_png_and_webp_with_gps_are_stored_without_metadata()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var png = TestImages.TinyPng(withGps: true);
        var webp = TestImages.Webp(withGps: true);
        using (var sentPng = new MagickImage(png))
        using (var sentWebp = new MagickImage(webp))
        {
            // Os arquivos enviados têm GPS (e o PNG, um comentário).
            Assert.NotNull(sentPng.GetExifProfile()?.GetValue(ExifTag.GPSLatitude));
            Assert.Equal(TestImages.Comment, sentPng.Comment);
            Assert.NotNull(sentWebp.GetExifProfile()?.GetValue(ExifTag.GPSLatitude));
        }

        // O PNG continua PNG (outro caminho de gravação que o do JPEG) e o WebP vira JPEG: nenhum dos dois leva metadado.
        var pngId = Id(await client.UploadAttachmentAsync(vehicleId, recordId, png, "peca.png"));
        var webpId = Id(await client.UploadAttachmentAsync(vehicleId, recordId, webp, "painel.webp"));
        var storedPng = await DownloadAsync(client, vehicleId, pngId);
        var storedWebp = await DownloadAsync(client, vehicleId, webpId);

        foreach (var (stored, format) in new[] { (storedPng, MagickFormat.Png), (storedWebp, MagickFormat.Jpeg) })
        {
            using var image = new MagickImage(stored);
            Assert.Equal(format, image.Format);
            Assert.Null(image.GetExifProfile());
            Assert.Empty(image.ProfileNames);
            Assert.Null(image.Comment);
            // Também nos bytes, sem depender da leitura da biblioteca: nem bloco EXIF nem blocos de texto do PNG.
            foreach (var marker in new[] { "Exif", "eXIf", "tEXt", "zTXt", "iTXt", TestImages.Comment })
                Assert.False(ContainsAscii(stored, marker), $"O arquivo gravado ({format}) ainda contém \"{marker}\".");
        }
    }

    [Fact]
    public async Task Upload_keeps_only_the_display_name_and_stores_the_file_under_the_system_key()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);

        // O nome enviado é só para exibição: pastas e ".." são descartados e nunca chegam ao caminho no disco.
        var unix = await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "../../../etc/foto.png");
        var windows = await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), @"..\..\fotos\peca.png");

        Assert.Equal("foto.png", unix.GetProperty("fileName").GetString());
        Assert.Equal("peca.png", windows.GetProperty("fileName").GetString());
        string[] expected = [StoredPath(vehicleId, Id(unix)), StoredPath(vehicleId, Id(windows))];
        Assert.Equal(expected.Order(StringComparer.Ordinal), StoredFiles(vehicleId).Order(StringComparer.Ordinal));

        using var download = await client.GetAsync(ContentUrl(vehicleId, Id(unix)));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("foto.png", download.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Upload_pdf_is_private_document_downloaded_as_attachment()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var pdf = TestImages.Pdf();

        // Pedido como foto: PDF é sempre documento, e documento nasce privado.
        var attachment = await client.UploadAttachmentAsync(vehicleId, recordId, pdf, "nota-fiscal-óleo.pdf", kind: "photo");

        Assert.Equal("document", attachment.GetProperty("kind").GetString());
        Assert.False(attachment.GetProperty("isPublic").GetBoolean());
        Assert.Equal("application/pdf", attachment.GetProperty("contentType").GetString());
        Assert.Equal(pdf.Length, attachment.GetProperty("sizeBytes").GetInt64());

        using var download = await client.GetAsync(ContentUrl(vehicleId, Id(attachment)));

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        var disposition = download.Content.Headers.ContentDisposition;
        Assert.Equal("attachment", disposition?.DispositionType);
        Assert.Equal("nota-fiscal-óleo.pdf", disposition?.FileNameStar);
        // PDFs são gravados como recebidos.
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_heic_or_renamed_file_returns_415_and_stores_nothing()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        (byte[] Content, string Name)[] rejected =
        [
            (TestImages.Heic(), "foto.jpg"),
            (Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"), "foto.png"),
            (Encoding.UTF8.GetBytes("<html><body>oi</body></html>"), "nota.pdf"),
            // Assinatura de JPEG, mas o resto não é uma imagem: o formato vale pelo conteúdo inteiro, não pelo cabeçalho.
            ([0xFF, 0xD8, 0xFF, .. Encoding.ASCII.GetBytes("isto nao e um jpeg de verdade")], "falso.jpg"),
            ([], "vazio.jpg")
        ];

        foreach (var (content, name) in rejected)
        {
            // O Content-Type que o cliente declara também não conta.
            using var response = await client.PostAttachmentAsync(vehicleId, recordId, content, name, contentType: "image/jpeg");

            await response.AssertProblemAsync(HttpStatusCode.UnsupportedMediaType, "attachment.unsupported_type");
        }

        Assert.Equal(0, (await client.GetRecordAsync(vehicleId, recordId)).GetProperty("attachments").GetArrayLength());
        AssertNoFilesStored(vehicleId);
    }

    [Fact]
    public async Task Upload_over_10mb_returns_413()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var content = new byte[Attachment.MaxSizeBytes + 1];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;

        using var response = await client.PostAttachmentAsync(vehicleId, recordId, content, "grande.jpg");

        await response.AssertProblemAsync(HttpStatusCode.RequestEntityTooLarge, "attachment.too_large");
        Assert.Equal(0, (await client.GetRecordAsync(vehicleId, recordId)).GetProperty("attachments").GetArrayLength());
        AssertNoFilesStored(vehicleId);
    }

    [Theory]
    [InlineData(12, true)]
    [InlineData(25, true)]
    [InlineData(12, false)]
    public async Task Upload_far_over_10mb_returns_413_too_large_and_stores_nothing(int megabytes, bool withContentLength)
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        // O caso comum (foto de câmera, PDF escaneado): bem acima do teto da requisição (10 MB do arquivo + 1 MB de folga).
        // Sem Content-Length (envio em partes), o teto só aparece durante a leitura do corpo.
        var content = new byte[megabytes * 1024 * 1024];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;

        using var response = withContentLength
            ? await client.PostAttachmentAsync(vehicleId, recordId, content, "enorme.jpg")
            : await PostWithoutContentLengthAsync(client, vehicleId, recordId, content, "enorme.jpg");

        await response.AssertProblemAsync(HttpStatusCode.RequestEntityTooLarge, "attachment.too_large");
        Assert.Equal(0, (await client.GetRecordAsync(vehicleId, recordId)).GetProperty("attachments").GetArrayLength());
        AssertNoFilesStored(vehicleId);
    }

    [Fact]
    public async Task Twenty_first_attachment_returns_422()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var png = TestImages.TinyPng();
        for (var i = 1; i <= Attachment.MaxPerRecord; i++)
            await client.UploadAttachmentAsync(vehicleId, recordId, png, $"foto-{i}.png");

        using var response = await client.PostAttachmentAsync(vehicleId, recordId, png, "foto-21.png");

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "attachment.limit_reached");
        Assert.Equal(20, (await client.GetRecordAsync(vehicleId, recordId)).GetProperty("attachments").GetArrayLength());
        Assert.Equal(20, StoredFiles(vehicleId).Length);

        // O limite é por registro: outro registro do mesmo veículo ainda aceita.
        var otherRecord = await client.CreateRecordAsync(vehicleId);
        await client.UploadAttachmentAsync(vehicleId, otherRecord, png, "outra.png");
    }

    [Fact]
    public async Task Upload_without_valid_kind_or_file_returns_400_and_stores_nothing()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var photo = TestImages.TinyPng();

        // Sem tipo não há padrão: uma foto de nota fiscal não pode virar pública por omissão.
        foreach (var kind in new[] { null, "", "banana", "0", "1" })
        {
            using var response = await client.PostAttachmentAsync(vehicleId, recordId, photo, "nota.png", kind);

            var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
            Assert.Equal(new[] { "kind" }, FieldNames(errors));
        }

        using var withoutFile = await client.PostAttachmentAsync(vehicleId, recordId, content: null, "nada.png");

        var fileErrors = (await withoutFile.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "file" }, FieldNames(fileErrors));

        // Os nomes valem sem diferenciar maiúsculas, como os filtros da lista de registros.
        var document = await client.UploadAttachmentAsync(vehicleId, recordId, photo, "nota.png", kind: "Document");
        Assert.Equal("document", document.GetProperty("kind").GetString());
        Assert.Single(StoredFiles(vehicleId));
    }

    [Fact]
    public async Task Upload_without_a_readable_multipart_form_is_rejected_and_stores_nothing()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var url = $"/api/vehicles/{vehicleId}/records/{recordId}/attachments";

        // Outro Content-Type: o roteamento (Accepts multipart/form-data) recusa antes do endpoint.
        using var json = await client.PostAsJsonAsync(url, new { kind = "photo" }, TestJson.Options);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, json.StatusCode);

        // Sem corpo: faltam o arquivo e o tipo.
        using var empty = await client.PostAsync(url, content: null);
        var emptyErrors = (await empty.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "file", "kind" }, FieldNames(emptyErrors));

        // Multipart malformado (o limite declarado nunca aparece no corpo): o arquivo não pôde ser lido.
        using var malformed = new StringContent("isto nao e um multipart", Encoding.UTF8);
        malformed.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=fronteira");
        using var broken = await client.PostAsync(url, malformed);
        var brokenErrors = (await broken.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "file" }, FieldNames(brokenErrors));

        AssertNoFilesStored(vehicleId);
    }

    [Fact]
    public async Task Making_document_public_returns_422()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var pdf = await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.Pdf(), "nota.pdf", kind: "document");
        // Foto de nota fiscal enviada como documento: é imagem, mas continua privada.
        var invoicePhoto = await client.UploadAttachmentAsync(
            vehicleId, recordId, TestImages.Jpeg(400, 300, withGps: false), "nota.jpg", kind: "document");
        Assert.Equal("document", invoicePhoto.GetProperty("kind").GetString());
        Assert.False(invoicePhoto.GetProperty("isPublic").GetBoolean());

        foreach (var id in new[] { Id(pdf), Id(invoicePhoto) })
        {
            using var response = await client.PatchAsJsonAsync(AttachmentUrl(vehicleId, id), new { isPublic = true }, TestJson.Options);

            await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "attachment.document_cannot_be_public");
            // Documento pode continuar privado.
            using var keepPrivate = await client.PatchAsJsonAsync(AttachmentUrl(vehicleId, id), new { isPublic = false }, TestJson.Options);
            Assert.Equal(HttpStatusCode.NoContent, keepPrivate.StatusCode);
        }

        var photo = await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "foto.png");
        using var hide = await client.PatchAsJsonAsync(AttachmentUrl(vehicleId, Id(photo)), new { isPublic = false }, TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);
        Assert.False(await IsPublicAsync(client, vehicleId, recordId, Id(photo)));
        using var show = await client.PatchAsJsonAsync(AttachmentUrl(vehicleId, Id(photo)), new { isPublic = true }, TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, show.StatusCode);
        Assert.True(await IsPublicAsync(client, vehicleId, recordId, Id(photo)));

        Assert.False(await IsPublicAsync(client, vehicleId, recordId, Id(pdf)));
        Assert.False(await IsPublicAsync(client, vehicleId, recordId, Id(invoicePhoto)));

        // Sem isPublic no corpo não há padrão.
        using var missing = await client.PatchAsJsonAsync(AttachmentUrl(vehicleId, Id(photo)), new { }, TestJson.Options);
        var errors = (await missing.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "isPublic" }, FieldNames(errors));
        Assert.True(await IsPublicAsync(client, vehicleId, recordId, Id(photo)));
    }

    [Fact]
    public async Task Attachment_of_other_vehicle_via_my_vehicle_url_returns_404()
    {
        using var client = await UserAsync();
        var (vehicleA, _) = await VehicleWithRecordAsync(client);
        var (vehicleB, recordB) = await VehicleWithRecordAsync(client);
        var attachmentB = Id(await client.UploadAttachmentAsync(vehicleB, recordB, TestImages.TinyPng(), "b.png"));

        // Os anexos são sempre buscados por veículo e id: o de B não é alcançável pela URL de A, mesmo sendo do mesmo dono.
        using var download = await client.GetAsync(ContentUrl(vehicleA, attachmentB));
        using var patch = await client.PatchAsJsonAsync(AttachmentUrl(vehicleA, attachmentB), new { isPublic = false }, TestJson.Options);
        using var delete = await client.DeleteAsync(AttachmentUrl(vehicleA, attachmentB));
        using var unknown = await client.GetAsync(ContentUrl(vehicleA, Guid.NewGuid()));
        using var uploadToRecordOfB = await client.PostAttachmentAsync(vehicleA, recordB, TestImages.TinyPng(), "a.png");

        await download.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        await patch.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        await delete.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        await uploadToRecordOfB.AssertProblemAsync(HttpStatusCode.NotFound, "record.not_found");
        Assert.Empty(StoredFiles(vehicleA));
        Assert.True(await IsPublicAsync(client, vehicleB, recordB, attachmentB));
        Assert.NotEmpty(await DownloadAsync(client, vehicleB, attachmentB));
    }

    [Fact]
    public async Task Other_user_cannot_download_or_delete()
    {
        using var owner = await UserAsync();
        using var other = await factory.CreateUserClientAsync();
        using var anonymous = await factory.CreateAnonymousClientAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(owner);
        var id = Id(await owner.UploadAttachmentAsync(vehicleId, recordId, TestImages.Pdf(), "nota.pdf", kind: "document"));

        using var download = await other.GetAsync(ContentUrl(vehicleId, id));
        using var patch = await other.PatchAsJsonAsync(AttachmentUrl(vehicleId, id), new { isPublic = false }, TestJson.Options);
        using var delete = await other.DeleteAsync(AttachmentUrl(vehicleId, id));
        using var upload = await other.PostAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "intruso.png");
        using var anonymousDownload = await anonymous.GetAsync(ContentUrl(vehicleId, id));

        await download.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await patch.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await delete.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await upload.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousDownload.StatusCode);
        Assert.Equal(TestImages.Pdf(), await DownloadAsync(owner, vehicleId, id));
        Assert.Single(StoredFiles(vehicleId));
    }

    [Fact]
    public async Task Record_detail_lists_attachments_and_list_counts_them()
    {
        using var client = await UserAsync();
        var (vehicleId, withAttachments) = await VehicleWithRecordAsync(client);
        var without = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Sem anexos"));
        var photo = await client.UploadAttachmentAsync(vehicleId, withAttachments, TestImages.TinyPng(), "foto.png");
        var pdf = await client.UploadAttachmentAsync(vehicleId, withAttachments, TestImages.Pdf(), "nota.pdf", kind: "document");

        var record = await client.GetRecordAsync(vehicleId, withAttachments);

        var attachments = record.GetProperty("attachments").EnumerateArray().ToDictionary(Id);
        Assert.Equal(2, attachments.Count);
        AssertSameAttachment(photo, attachments[Id(photo)]);
        AssertSameAttachment(pdf, attachments[Id(pdf)]);
        Assert.Equal(0, (await client.GetRecordAsync(vehicleId, without)).GetProperty("attachments").GetArrayLength());

        var counts = (await client.GetRecordsAsync(vehicleId)).GetProperty("items").EnumerateArray()
            .ToDictionary(Id, item => item.GetProperty("attachmentCount").GetInt32());
        Assert.Equal(2, counts[withAttachments]);
        Assert.Equal(0, counts[without]);
    }

    [Fact]
    public async Task Deleting_attachment_removes_its_file_from_disk()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var removed = Id(await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "sai.png"));
        var kept = Id(await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "fica.png"));
        Assert.True(File.Exists(StoredPath(vehicleId, removed)));

        using var deleted = await client.DeleteAsync(AttachmentUrl(vehicleId, removed));
        using var download = await client.GetAsync(ContentUrl(vehicleId, removed));
        using var deletedAgain = await client.DeleteAsync(AttachmentUrl(vehicleId, removed));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await download.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        await deletedAgain.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        Assert.False(File.Exists(StoredPath(vehicleId, removed)));
        Assert.True(File.Exists(StoredPath(vehicleId, kept)));
        var remaining = Assert.Single((await client.GetRecordAsync(vehicleId, recordId)).GetProperty("attachments").EnumerateArray());
        Assert.Equal(kept, Id(remaining));
    }

    [Fact]
    public async Task Deleting_record_removes_files_from_disk()
    {
        using var client = await UserAsync();
        var (vehicleId, removedRecord) = await VehicleWithRecordAsync(client);
        var keptRecord = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Fica"));
        Guid[] removed =
        [
            Id(await client.UploadAttachmentAsync(vehicleId, removedRecord, TestImages.TinyPng(), "a.png")),
            Id(await client.UploadAttachmentAsync(vehicleId, removedRecord, TestImages.Pdf(), "b.pdf", kind: "document"))
        ];
        var kept = Id(await client.UploadAttachmentAsync(vehicleId, keptRecord, TestImages.TinyPng(), "c.png"));
        Assert.All(removed, id => Assert.True(File.Exists(StoredPath(vehicleId, id))));

        using var deleted = await client.DeleteAsync($"/api/vehicles/{vehicleId}/records/{removedRecord}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.All(removed, id => Assert.False(File.Exists(StoredPath(vehicleId, id))));
        foreach (var id in removed)
        {
            using var download = await client.GetAsync(ContentUrl(vehicleId, id));
            await download.AssertProblemAsync(HttpStatusCode.NotFound, "attachment.not_found");
        }

        Assert.True(File.Exists(StoredPath(vehicleId, kept)));
        Assert.NotEmpty(await DownloadAsync(client, vehicleId, kept));
    }

    [Fact]
    public async Task Deleting_vehicle_removes_files_from_disk()
    {
        using var client = await UserAsync();
        var (vehicleId, firstRecord) = await VehicleWithRecordAsync(client);
        var secondRecord = await client.CreateRecordAsync(vehicleId, TestData.Record(title: "Segundo"));
        var (otherVehicle, otherRecord) = await VehicleWithRecordAsync(client);
        await client.UploadAttachmentAsync(vehicleId, firstRecord, TestImages.TinyPng(), "a.png");
        await client.UploadAttachmentAsync(vehicleId, secondRecord, TestImages.Pdf(), "b.pdf", kind: "document");
        var kept = Id(await client.UploadAttachmentAsync(otherVehicle, otherRecord, TestImages.TinyPng(), "c.png"));
        Assert.Equal(2, StoredFiles(vehicleId).Length);

        using var deleted = await client.DeleteAsync($"/api/vehicles/{vehicleId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        AssertNoFilesStored(vehicleId);
        Assert.True(File.Exists(StoredPath(otherVehicle, kept)));
    }

    [Fact]
    public async Task Responses_have_nosniff_header()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var photo = Id(await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "foto.png"));
        var pdf = Id(await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.Pdf(), "nota.pdf", kind: "document"));

        using var image = await client.GetAsync(ContentUrl(vehicleId, photo));
        using var document = await client.GetAsync(ContentUrl(vehicleId, pdf));
        using var missing = await client.GetAsync(ContentUrl(vehicleId, Guid.NewGuid()));

        foreach (var response in new[] { image, document, missing })
            Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
    }

    [Fact]
    public async Task Private_downloads_are_never_cached()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        var photo = Id(await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "foto.png"));
        var pdf = Id(await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.Pdf(), "nota.pdf", kind: "document"));

        using var image = await client.GetAsync(ContentUrl(vehicleId, photo));
        using var document = await client.GetAsync(ContentUrl(vehicleId, pdf));

        // Nota fiscal e foto privada do dono: nada fica em cache de navegador nem de proxy.
        foreach (var response in new[] { image, document })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var cache = response.Headers.CacheControl;
            Assert.NotNull(cache);
            Assert.True(cache.Private);
            Assert.True(cache.NoStore);
        }
    }

    [Fact]
    public async Task Upload_requires_the_antiforgery_token()
    {
        using var client = await UserAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        using var response = await client.PostAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "foto.png");

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "auth.antiforgery_invalid");
        AssertNoFilesStored(vehicleId);
    }

    [Fact]
    public async Task Uploads_are_rate_limited_per_user()
    {
        factory.Clock.SetUtcNow(Now);
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:UploadsPerMinute", "1"));
        using var client = await limited.CreateUserClientAsync();
        var (vehicleId, recordId) = await VehicleWithRecordAsync(client);
        await client.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "primeira.png");

        using var second = await client.PostAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "segunda.png");

        await second.AssertProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
        Assert.Single(StoredFiles(vehicleId));
    }

    private async Task<HttpClient> UserAsync()
    {
        factory.Clock.SetUtcNow(Now);
        return await factory.CreateUserClientAsync();
    }

    private static async Task<(Guid VehicleId, Guid RecordId)> VehicleWithRecordAsync(HttpClient client)
    {
        var vehicleId = await client.CreateVehicleAsync();
        return (vehicleId, await client.CreateRecordAsync(vehicleId));
    }

    private static string AttachmentUrl(Guid vehicleId, Guid id) => $"/api/vehicles/{vehicleId}/attachments/{id}";

    private static string ContentUrl(Guid vehicleId, Guid id) => $"{AttachmentUrl(vehicleId, id)}/content";

    private static Guid Id(JsonElement element) => element.GetProperty("id").GetGuid();

    private static async Task<byte[]> DownloadAsync(HttpClient client, Guid vehicleId, Guid id)
    {
        using var response = await client.GetAsync(ContentUrl(vehicleId, id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static async Task<bool> IsPublicAsync(HttpClient client, Guid vehicleId, Guid recordId, Guid id) =>
        (await client.GetRecordAsync(vehicleId, recordId)).GetProperty("attachments").EnumerateArray()
        .Single(attachment => Id(attachment) == id)
        .GetProperty("isPublic").GetBoolean();

    private static void AssertSameAttachment(JsonElement expected, JsonElement actual)
    {
        foreach (var field in new[] { "kind", "fileName", "contentType" })
            Assert.Equal(expected.GetProperty(field).GetString(), actual.GetProperty(field).GetString());
        Assert.Equal(expected.GetProperty("isPublic").GetBoolean(), actual.GetProperty("isPublic").GetBoolean());
        Assert.Equal(expected.GetProperty("sizeBytes").GetInt64(), actual.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(expected.GetProperty("uploadedAt").GetDateTimeOffset(), actual.GetProperty("uploadedAt").GetDateTimeOffset());
    }

    /// <summary>Caminho no disco do arquivo de um anexo: a chave é <c>{vehicleId}/{attachmentId}</c>, sob <c>Storage:RootPath</c>.</summary>
    private string StoredPath(Guid vehicleId, Guid id) => Path.Combine(factory.StoragePath, vehicleId.ToString(), id.ToString());

    private string[] StoredFiles(Guid vehicleId)
    {
        var folder = Path.Combine(factory.StoragePath, vehicleId.ToString());
        return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    }

    private void AssertNoFilesStored(Guid vehicleId)
    {
        var folder = Path.Combine(factory.StoragePath, vehicleId.ToString());
        Assert.True(!Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any(),
            $"Esperava a pasta {folder} vazia ou inexistente.");
    }

    private static async Task<HttpResponseMessage> PostWithoutContentLengthAsync(
        HttpClient client, Guid vehicleId, Guid recordId, byte[] content, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new UnknownLengthContent(content), "file", fileName);
        form.Add(new StringContent("photo"), "kind");
        Assert.Null(form.Headers.ContentLength); // o corpo vai em partes (chunked)

        return await client.PostAsync($"/api/vehicles/{vehicleId}/records/{recordId}/attachments", form);
    }

    private static bool ContainsAscii(byte[] content, string text) =>
        content.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;

    private static string[] FieldNames(JsonElement errors) =>
        errors.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Conteúdo sem tamanho conhecido: o cliente não envia Content-Length.</summary>
    private sealed class UnknownLengthContent(byte[] content) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
