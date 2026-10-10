using System.Text;
using Garage.Domain.Attachments;

namespace Garage.Domain.Tests.Attachments;

public class FileSignatureTests
{
    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Webp(string brand) =>
        [.. Ascii("RIFF"), 0x00, 0x00, 0x00, 0x00, .. Ascii(brand)];

    public static TheoryData<byte[], DetectedFileType?> Signatures => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01], DetectedFileType.Jpeg },
        { [0xFF, 0xD8, 0xFF], DetectedFileType.Jpeg },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D], DetectedFileType.Png },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], DetectedFileType.Png },
        { Webp("WEBP"), DetectedFileType.Webp },
        { Ascii("%PDF-1.7"), DetectedFileType.Pdf },
        { Ascii("%PDF-"), DetectedFileType.Pdf },
        { [0x00, 0x00, 0x00, 0x18, .. Ascii("ftypheic")], null },
        { Ascii("<svg"), null },
        { Webp("WAVE"), null },
        { Ascii("GIF89a"), null },
        { [], null }
    };

    public static TheoryData<byte[]> TruncatedSignatures => new()
    {
        { [0xFF, 0xD8] },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A] },
        { [.. Ascii("RIFF"), 0x00, 0x00, 0x00, 0x00, .. Ascii("WEB")] },
        { Ascii("%PDF") }
    };

    [Theory]
    [MemberData(nameof(Signatures))]
    public void Detects_supported_types(byte[] header, DetectedFileType? expected)
    {
        Assert.Equal(expected, FileSignature.Detect(header));
    }

    [Theory]
    [MemberData(nameof(TruncatedSignatures))]
    public void Header_shorter_than_the_signature_is_not_detected(byte[] header)
    {
        Assert.Null(FileSignature.Detect(header));
    }

    [Fact]
    public void Header_length_covers_the_longest_signature()
    {
        Assert.Equal(12, FileSignature.HeaderLength);
    }

    [Fact]
    public void Only_the_first_header_bytes_are_inspected()
    {
        var jpegWithLongBody = new byte[FileSignature.HeaderLength * 4];
        jpegWithLongBody[0] = 0xFF;
        jpegWithLongBody[1] = 0xD8;
        jpegWithLongBody[2] = 0xFF;

        Assert.Equal(DetectedFileType.Jpeg, FileSignature.Detect(jpegWithLongBody));
    }
}
