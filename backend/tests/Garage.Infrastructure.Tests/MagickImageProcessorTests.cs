using Garage.Domain.Attachments;
using Garage.Infrastructure.Storage;
using ImageMagick;

namespace Garage.Infrastructure.Tests;

public sealed class MagickImageProcessorTests
{
    [Fact]
    public async Task Photo_with_a_wide_gamut_profile_is_converted_to_srgb_before_the_metadata_is_removed()
    {
        // Um PNG em Adobe RGB (1998), com EXIF: os mesmos números de pixel valem cores mais saturadas que em sRGB. Sem o
        // perfil, o navegador lê os números como sRGB e a foto sai desbotada; por isso os pixels precisam ser convertidos.
        using var original = new MagickImage(MagickColor.FromRgb(200, 100, 50), 8, 8);
        original.SetProfile(ColorProfiles.AdobeRGB1998);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Software, "teste");
        original.SetProfile(exif);
        original.Format = MagickFormat.Png;
        var bytes = original.ToByteArray();

        // A cor que um navegador mostraria para o original, com o perfil.
        using var reference = new MagickImage(bytes);
        reference.TransformColorSpace(ColorProfiles.SRGB);

        using var input = new MemoryStream(bytes);
        var processed = await new MagickImageProcessor().NormalizeAsync(input, DetectedFileType.Png, CancellationToken.None);

        using var result = new MagickImage(processed.Content);
        Assert.Null(result.GetColorProfile());
        Assert.Null(result.GetExifProfile());
        var (r, g, b) = Center(result);
        var (expectedR, expectedG, expectedB) = Center(reference);
        Assert.NotEqual((200, 100, 50), (r, g, b));
        Assert.InRange(Math.Abs(r - expectedR), 0, 2);
        Assert.InRange(Math.Abs(g - expectedG), 0, 2);
        Assert.InRange(Math.Abs(b - expectedB), 0, 2);
    }

    [Fact]
    public async Task Photo_without_a_color_profile_keeps_its_pixels()
    {
        using var original = new MagickImage(MagickColor.FromRgb(200, 100, 50), 8, 8);
        original.Format = MagickFormat.Png;
        using var input = new MemoryStream(original.ToByteArray());

        var processed = await new MagickImageProcessor().NormalizeAsync(input, DetectedFileType.Png, CancellationToken.None);

        using var result = new MagickImage(processed.Content);
        Assert.Null(result.GetColorProfile());
        Assert.Equal((200, 100, 50), Center(result));
    }

    private static (int R, int G, int B) Center(MagickImage image)
    {
        using var pixels = image.GetPixels();
        var color = pixels.GetPixel((int)image.Width / 2, (int)image.Height / 2).ToColor()!;
        return (color.R, color.G, color.B);
    }
}
