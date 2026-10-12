using Garage.Application.Abstractions.Ports;
using Garage.Domain.Attachments;
using ImageMagick;

namespace Garage.Infrastructure.Storage;

/// <summary>
/// A5 com Magick.NET: orientação corrigida, cores convertidas para sRGB, metadados removidos, maior lado ≤ 2560 px; PNG fica
/// PNG, o resto vira JPEG 85.
/// </summary>
internal sealed class MagickImageProcessor : IImageProcessor
{
    internal const uint MaxSide = 2560;
    internal const uint JpegQuality = 85;

    /// <summary>
    /// Teto de pixels para decodificar (64 MP cobre as fotos de 50 MP dos celulares). Um PNG de poucos KB pode declarar
    /// dimensões enormes e consumir gigabytes ao ser aberto; as dimensões são lidas do cabeçalho antes.
    /// </summary>
    internal const ulong MaxPixels = 64_000_000;

    private const string JpegContentType = "image/jpeg";
    private const string PngContentType = "image/png";

    public Task<ProcessedFile> NormalizeAsync(Stream input, DetectedFileType type, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        // O formato é o detectado pelos primeiros bytes: a biblioteca nunca escolhe o leitor pelo conteúdo (SVG, MVG...).
        var settings = new MagickReadSettings { Format = ToMagickFormat(type) };
        var start = input.Position;
        try
        {
            var info = new MagickImageInfo(input, settings);
            if ((ulong)info.Width * info.Height > MaxPixels)
                throw new InvalidDataException($"Imagem com {info.Width}×{info.Height} pixels passa do limite de {MaxPixels}.");

            input.Position = start;
            using var image = new MagickImage(input, settings);

            // A orientação vem do EXIF: precisa ser aplicada antes de Strip removê-lo.
            image.AutoOrient();
            // O perfil de cor também sai no Strip: uma foto em Display P3 ou Adobe RGB teria os números de pixel lidos como
            // sRGB e ficaria desbotada. Converter antes deixa as cores certas sem perfil nenhum.
            if (image.GetColorProfile() is not null)
                image.TransformColorSpace(ColorProfiles.SRGB);
            image.Strip();
            image.Resize(new MagickGeometry(MaxSide, MaxSide) { Greater = true });

            var contentType = type == DetectedFileType.Png ? PngContentType : JpegContentType;
            if (type == DetectedFileType.Png)
            {
                image.Format = MagickFormat.Png;
            }
            else
            {
                // JPEG não tem transparência: o fundo transparente de um WebP fica branco, não preto.
                if (image.HasAlpha)
                {
                    image.BackgroundColor = MagickColors.White;
                    image.Alpha(AlphaAction.Remove);
                }

                image.Format = MagickFormat.Jpeg;
                image.Quality = JpegQuality;
            }

            var output = new MemoryStream();
            image.Write(output);
            output.Position = 0;
            return Task.FromResult(new ProcessedFile(output, contentType, output.Length));
        }
        catch (MagickException ex)
        {
            throw new InvalidDataException($"O conteúdo não é uma imagem {type} válida.", ex);
        }
    }

    private static MagickFormat ToMagickFormat(DetectedFileType type) => type switch
    {
        DetectedFileType.Jpeg => MagickFormat.Jpeg,
        DetectedFileType.Png => MagickFormat.Png,
        DetectedFileType.Webp => MagickFormat.WebP,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Somente imagens são reprocessadas.")
    };
}
