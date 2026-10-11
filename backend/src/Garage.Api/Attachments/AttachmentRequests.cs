namespace Garage.Api.Attachments;

/// <summary>Corpo do <c>PATCH</c> de um anexo. <see cref="IsPublic"/> é obrigatório: ausente não vira <c>false</c>.</summary>
public sealed record SetAttachmentVisibilityRequest(bool? IsPublic);
