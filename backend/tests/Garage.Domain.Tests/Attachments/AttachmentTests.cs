using Garage.Domain.Attachments;
using Garage.Domain.Common;

namespace Garage.Domain.Tests.Attachments;

public class AttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid VehicleId = Guid.Parse("0197b3a0-0000-7000-8000-000000000002");
    private static readonly Guid RecordId = Guid.Parse("0197b3a0-0000-7000-8000-000000000003");

    private static Attachment Create(
        AttachmentKind requestedKind = AttachmentKind.Photo,
        DetectedFileType type = DetectedFileType.Jpeg,
        string fileName = "oleo.jpg",
        string contentType = "image/jpeg",
        long sizeBytes = 204_800) =>
        Attachment.Create(VehicleId, RecordId, requestedKind, type, fileName, contentType, sizeBytes, Now);

    [Fact]
    public void Limits_match_the_spec()
    {
        Assert.Equal(10L * 1024 * 1024, Attachment.MaxSizeBytes);
        Assert.Equal(20, Attachment.MaxPerRecord);
    }

    [Fact]
    public void Create_populates_fields_and_generates_a_version7_id()
    {
        var attachment = Create();

        Assert.Equal(7, attachment.Id.Version);
        Assert.Equal(VehicleId, attachment.VehicleId);
        Assert.Equal(RecordId, attachment.RecordId);
        Assert.Equal(AttachmentKind.Photo, attachment.Kind);
        Assert.Equal("oleo.jpg", attachment.FileName);
        Assert.Equal("image/jpeg", attachment.ContentType);
        Assert.Equal(204_800, attachment.SizeBytes);
        Assert.Equal(Now, attachment.UploadedAt);
    }

    [Theory]
    [InlineData(DetectedFileType.Jpeg)]
    [InlineData(DetectedFileType.Png)]
    [InlineData(DetectedFileType.Webp)]
    public void Photo_is_public_by_default(DetectedFileType type)
    {
        var attachment = Create(AttachmentKind.Photo, type);

        Assert.Equal(AttachmentKind.Photo, attachment.Kind);
        Assert.True(attachment.IsPublic);
    }

    [Fact]
    public void Image_marked_as_document_is_private()
    {
        var attachment = Create(AttachmentKind.Document, DetectedFileType.Jpeg);

        Assert.Equal(AttachmentKind.Document, attachment.Kind);
        Assert.False(attachment.IsPublic);
    }

    [Theory]
    [InlineData(AttachmentKind.Photo)]
    [InlineData(AttachmentKind.Document)]
    public void Pdf_is_always_private_document(AttachmentKind requestedKind)
    {
        var attachment = Create(requestedKind, DetectedFileType.Pdf, "nota.pdf", "application/pdf");

        Assert.Equal(AttachmentKind.Document, attachment.Kind);
        Assert.False(attachment.IsPublic);
    }

    [Fact]
    public void Document_cannot_be_made_public()
    {
        var attachment = Create(AttachmentKind.Document, DetectedFileType.Pdf, "nota.pdf", "application/pdf");

        var result = attachment.SetVisibility(true);

        Assert.True(result.IsFailure);
        Assert.Equal("attachment.document_cannot_be_public", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
        Assert.False(attachment.IsPublic);
    }

    [Fact]
    public void Document_can_stay_private()
    {
        var attachment = Create(AttachmentKind.Document, DetectedFileType.Png);

        var result = attachment.SetVisibility(false);

        Assert.True(result.IsSuccess);
        Assert.False(attachment.IsPublic);
    }

    [Fact]
    public void Photo_visibility_can_be_toggled()
    {
        var attachment = Create();

        var hide = attachment.SetVisibility(false);
        Assert.True(hide.IsSuccess);
        Assert.False(attachment.IsPublic);

        var show = attachment.SetVisibility(true);
        Assert.True(show.IsSuccess);
        Assert.True(attachment.IsPublic);
    }

    [Fact]
    public void StorageKey_is_vehicle_slash_id()
    {
        var attachment = Create();

        Assert.Equal($"{VehicleId}/{attachment.Id}", attachment.StorageKey);
        Assert.Matches(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            attachment.StorageKey);
    }
}

public class AttachmentErrorsTests
{
    [Fact]
    public void NotFound_is_a_not_found_error_in_pt_BR()
    {
        var error = AttachmentErrors.NotFound;

        Assert.Equal("attachment.not_found", error.Code);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("Anexo não encontrado.", error.Message);
    }

    [Fact]
    public void TooLarge_is_a_payload_too_large_error_in_pt_BR()
    {
        var error = AttachmentErrors.TooLarge;

        Assert.Equal("attachment.too_large", error.Code);
        Assert.Equal(ErrorType.PayloadTooLarge, error.Type);
        Assert.Equal("Arquivo maior que 10 MB.", error.Message);
    }

    [Fact]
    public void UnsupportedType_is_an_unsupported_media_error_in_pt_BR()
    {
        var error = AttachmentErrors.UnsupportedType;

        Assert.Equal("attachment.unsupported_type", error.Code);
        Assert.Equal(ErrorType.UnsupportedMedia, error.Type);
        Assert.Equal("Formato não suportado. Envie JPG, PNG, WebP ou PDF.", error.Message);
    }

    [Fact]
    public void LimitReached_is_a_domain_rule_error_in_pt_BR()
    {
        var error = AttachmentErrors.LimitReached;

        Assert.Equal("attachment.limit_reached", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("Limite de 20 anexos por registro atingido.", error.Message);
    }

    [Fact]
    public void DocumentCannotBePublic_is_a_domain_rule_error_in_pt_BR()
    {
        var error = AttachmentErrors.DocumentCannotBePublic;

        Assert.Equal("attachment.document_cannot_be_public", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("Documentos não podem ficar visíveis no link público.", error.Message);
    }
}
