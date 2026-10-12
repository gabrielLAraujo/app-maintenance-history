using Garage.Domain.Attachments;
using Garage.Infrastructure.Demo;

namespace Garage.Infrastructure.Tests.Demo;

public sealed class EmbeddedDemoAssetsTests
{
    // Os arquivos de Demo/Assets que o catálogo de exemplo anexa: se o .csproj deixar algum fora do build, falha aqui.
    public static TheoryData<string, DetectedFileType> Assets => new()
    {
        { "civic-correia-dentada.jpg", DetectedFileType.Jpeg },
        { "civic-pastilhas-freio.jpg", DetectedFileType.Jpeg },
        { "civic-alinhamento.jpg", DetectedFileType.Jpeg },
        { "civic-coilover.jpg", DetectedFileType.Jpeg },
        { "civic-amortecedores-originais.jpg", DetectedFileType.Jpeg },
        { "civic-coilover-nota-exemplo.pdf", DetectedFileType.Pdf },
        { "civic-oleo.jpg", DetectedFileType.Jpeg },
        { "civic-filtro-oleo.jpg", DetectedFileType.Jpeg },
        { "civic-lx-frente.jpg", DetectedFileType.Jpeg },
        { "gol-pneus.jpg", DetectedFileType.Jpeg }
    };

    [Theory]
    [MemberData(nameof(Assets))]
    public async Task Open_returns_the_embedded_file(string name, DetectedFileType expected)
    {
        await using var stream = new EmbeddedDemoAssets().Open(name);

        var header = new byte[FileSignature.HeaderLength];
        await stream.ReadExactlyAsync(header);
        Assert.Equal(expected, FileSignature.Detect(header));
        Assert.True(stream.Length > header.Length);
    }

    [Fact]
    public void Open_throws_when_no_file_is_embedded_with_that_name()
    {
        var error = Assert.Throws<FileNotFoundException>(() => new EmbeddedDemoAssets().Open("nao-existe.jpg"));

        Assert.Contains("nao-existe.jpg", error.Message, StringComparison.Ordinal);
    }
}
