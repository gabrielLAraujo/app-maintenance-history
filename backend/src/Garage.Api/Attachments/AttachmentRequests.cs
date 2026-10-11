using Garage.Domain.Attachments;

namespace Garage.Api.Attachments;

/// <summary>
/// Formulário <c>multipart/form-data</c> do upload, descrito para o OpenAPI (o endpoint lê o formulário por conta própria):
/// <c>file</c> é o arquivo (JPEG, PNG, WebP ou PDF, até 10 MB) e <c>kind</c>, obrigatório, é <c>photo</c> ou <c>document</c>.
/// </summary>
public sealed record UploadAttachmentForm(IFormFile File, AttachmentKind Kind);

/// <summary>Corpo do <c>PATCH</c> de um anexo. <see cref="IsPublic"/> é obrigatório: ausente não vira <c>false</c>.</summary>
public sealed record SetAttachmentVisibilityRequest(bool? IsPublic);
