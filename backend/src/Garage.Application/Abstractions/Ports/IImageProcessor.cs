using Garage.Domain.Attachments;

namespace Garage.Application.Abstractions.Ports;

/// <summary>Conteúdo pronto para gravar, com o tipo e o tamanho finais. Quem recebe descarta o stream.</summary>
public sealed record ProcessedFile(Stream Content, string ContentType, long Length);

/// <summary>A5: as imagens são reprocessadas no servidor antes de gravar.</summary>
public interface IImageProcessor
{
    /// <summary>
    /// Corrige a orientação, remove todos os metadados (EXIF/GPS), limita o maior lado a 2560 px (só reduz) e regrava:
    /// PNG continua PNG; JPEG e WebP viram JPEG com qualidade 85. Lê o arquivo como o <paramref name="type"/> detectado
    /// pelos primeiros bytes, nunca pelo formato que a biblioteca adivinharia.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// O conteúdo não é uma imagem válida do tipo detectado, ou tem pixels demais para decodificar com segurança.
    /// </exception>
    Task<ProcessedFile> NormalizeAsync(Stream input, DetectedFileType type, CancellationToken ct);
}
