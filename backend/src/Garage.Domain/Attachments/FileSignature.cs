namespace Garage.Domain.Attachments;

public static class FileSignature
{
    /// <summary>Quantidade de bytes iniciais necessária para reconhecer qualquer formato aceito (a assinatura do WebP).</summary>
    public const int HeaderLength = 12;

    private const int WebpBrandOffset = 8;

    private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> RiffMagic => "RIFF"u8;

    private static ReadOnlySpan<byte> WebpBrand => "WEBP"u8;

    private static ReadOnlySpan<byte> PdfMagic => "%PDF-"u8;

    /// <summary>
    /// Reconhece o formato pelos primeiros bytes; devolve <c>null</c> quando nenhum formato aceito casa
    /// ou quando o trecho é curto demais para conter a assinatura (JPEG 3 bytes, PNG 8, WebP 12, PDF 5).
    /// </summary>
    public static DetectedFileType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegMagic))
            return DetectedFileType.Jpeg;

        if (header.StartsWith(PngMagic))
            return DetectedFileType.Png;

        if (IsWebp(header))
            return DetectedFileType.Webp;

        if (header.StartsWith(PdfMagic))
            return DetectedFileType.Pdf;

        return null;
    }

    private static bool IsWebp(ReadOnlySpan<byte> header) =>
        header.Length >= HeaderLength &&
        header.StartsWith(RiffMagic) &&
        header.Slice(WebpBrandOffset, WebpBrand.Length).SequenceEqual(WebpBrand);
}
