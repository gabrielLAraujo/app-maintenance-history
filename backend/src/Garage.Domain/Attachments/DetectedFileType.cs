namespace Garage.Domain.Attachments;

/// <summary>Formatos aceitos, reconhecidos pelos primeiros bytes do arquivo (nunca pelo nome ou pelo Content-Type enviado).</summary>
public enum DetectedFileType
{
    Jpeg,
    Png,
    Webp,
    Pdf
}
