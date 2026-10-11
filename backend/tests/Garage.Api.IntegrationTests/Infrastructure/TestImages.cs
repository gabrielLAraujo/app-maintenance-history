using System.Text;
using ImageMagick;

namespace Garage.Api.IntegrationTests.Infrastructure;

/// <summary>Arquivos de teste para os anexos, gerados em memória.</summary>
public static class TestImages
{
    /// <summary>Comentário gravado no PNG "com GPS": também é metadado e não pode sobreviver ao reprocessamento.</summary>
    public const string Comment = "tirada na oficina do Joao";

    /// <summary>
    /// JPEG <paramref name="w"/>×<paramref name="h"/>. Com <paramref name="withGps"/>, leva EXIF com a latitude de São Paulo;
    /// com <paramref name="orientation"/>, a orientação EXIF (6 = girar 90° no sentido horário para exibir).
    /// </summary>
    public static byte[] Jpeg(int w, int h, bool withGps, ushort? orientation = null)
    {
        using var image = new MagickImage(MagickColors.SteelBlue, (uint)w, (uint)h);

        var exif = new ExifProfile();
        if (withGps)
            AddGps(exif);

        if (orientation is { } value)
        {
            exif.SetValue(ExifTag.Orientation, value);
            image.Orientation = (Orientation)value;
        }

        if (exif.Values.Any())
            image.SetProfile(exif);

        return image.ToByteArray(MagickFormat.Jpeg);
    }

    /// <summary>PNG 2×2. Com <paramref name="withGps"/>, leva EXIF com a latitude de São Paulo e o <see cref="Comment"/>.</summary>
    public static byte[] TinyPng(bool withGps = false) => Image(MagickColors.Red, 2, 2, MagickFormat.Png, withGps);

    /// <summary>WebP 40×30. Com <paramref name="withGps"/>, leva EXIF com a latitude de São Paulo.</summary>
    public static byte[] Webp(bool withGps = false) => Image(MagickColors.Green, 40, 30, MagickFormat.WebP, withGps);

    /// <summary>PDF mínimo (só a assinatura e a estrutura vazia).</summary>
    public static byte[] Pdf() => Encoding.Latin1.GetBytes("%PDF-1.4\n%âãÏÓ\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");

    /// <summary>Cabeçalho de um HEIC (caixa <c>ftyp</c> com a marca <c>heic</c>), seguido de zeros.</summary>
    public static byte[] Heic() =>
    [
        0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63,
        .. new byte[52]
    ];

    private static byte[] Image(IMagickColor<byte> color, uint w, uint h, MagickFormat format, bool withGps)
    {
        using var image = new MagickImage(color, w, h);
        if (withGps)
        {
            var exif = new ExifProfile();
            AddGps(exif);
            image.SetProfile(exif);
            image.Comment = Comment;
        }

        return image.ToByteArray(format);
    }

    private static void AddGps(ExifProfile exif)
    {
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(23, 1), new Rational(33, 1), new Rational(0, 1)]);
        exif.SetValue(ExifTag.GPSLatitudeRef, "S");
    }
}
