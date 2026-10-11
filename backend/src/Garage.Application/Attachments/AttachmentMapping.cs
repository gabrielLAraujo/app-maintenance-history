using Garage.Domain.Attachments;

namespace Garage.Application.Attachments;

internal static class AttachmentMapping
{
    private const string DefaultFileName = "arquivo";

    // Extensões mais longas que isso não são extensões de verdade: o corte não tenta preservá-las.
    private const int MaxPreservedExtensionLength = 16;

    public static AttachmentDto ToDto(this Attachment a) =>
        new(a.Id, a.Kind, a.IsPublic, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAt);

    /// <summary>
    /// O nome original serve só para exibição e para o <c>Content-Disposition</c> do download (o arquivo no disco usa a
    /// chave do sistema). Fica sem pastas e sem caracteres de controle (o Postgres recusa <c>\0</c> em texto), aparado e
    /// com no máximo <see cref="Attachment.MaxFileNameLength"/> caracteres, preservando a extensão. Vazio vira "arquivo".
    /// </summary>
    public static string CleanFileName(string? fileName)
    {
        var name = fileName ?? string.Empty;
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        name = string.Concat(name.Where(c => !char.IsControl(c))).Trim();
        if (name.Length == 0)
            return DefaultFileName;
        if (name.Length <= Attachment.MaxFileNameLength)
            return name;

        var extension = Path.GetExtension(name);
        if (extension.Length > MaxPreservedExtensionLength)
            extension = string.Empty;

        var stemLength = Attachment.MaxFileNameLength - extension.Length;
        // Não corta um par substituto (emoji etc.) ao meio.
        if (char.IsHighSurrogate(name[stemLength - 1]))
            stemLength--;

        return name[..stemLength] + extension;
    }
}
