using System.Text;
using Garage.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Tests;

public sealed class LocalDiskFileStorageTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // A raiz fica dentro de uma pasta própria do teste, para conferir que nada é gravado ao lado dela.
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "garage-storage-tests", Guid.NewGuid().ToString("N"));
    private readonly LocalDiskFileStorage _storage;

    public LocalDiskFileStorageTests() =>
        _storage = new LocalDiskFileStorage(Options.Create(new StorageOptions { RootPath = Root }));

    private string Root => Path.Combine(_sandbox, "root");

    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
            Directory.Delete(_sandbox, recursive: true);
    }

    [Fact]
    public async Task Save_then_open_roundtrip()
    {
        var vehicleId = Guid.NewGuid().ToString();
        var key = $"{vehicleId}/{Guid.NewGuid()}";
        var content = Encoding.UTF8.GetBytes("conteúdo do anexo");

        await _storage.SaveAsync(key, new MemoryStream(content), Ct);

        Assert.Equal(content, await ReadAsync(key));
        // Grava num temporário e move: na pasta do veículo só fica o arquivo final.
        var file = Assert.Single(Directory.GetFiles(Path.Combine(Root, vehicleId)));
        Assert.Equal(Path.GetFullPath(Path.Combine(Root, key)), file);

        // Gravar de novo na mesma chave substitui o conteúdo.
        await _storage.SaveAsync(key, new MemoryStream([1, 2, 3]), Ct);
        Assert.Equal([1, 2, 3], await ReadAsync(key));

        await _storage.DeleteAsync(key, Ct);
        Assert.Null(await _storage.OpenReadAsync(key, Ct));

        // Ausentes: ler devolve null e apagar não falha, mesmo sem a pasta do veículo.
        await _storage.DeleteAsync(key, Ct);
        var missing = $"{Guid.NewGuid()}/{Guid.NewGuid()}";
        Assert.Null(await _storage.OpenReadAsync(missing, Ct));
        await _storage.DeleteAsync(missing, Ct);
    }

    [Fact]
    public async Task Rejects_keys_outside_root()
    {
        string[] keys =
        [
            "../fora.txt",
            "a/../../fora.txt",
            // Prefixo igual ao da raiz ("root" → "root-vizinha") não é estar dentro dela.
            "../root-vizinha/fora.txt",
            Path.Combine(_sandbox, "fora.txt"),
            "..",
            ".",
            ""
        ];

        foreach (var key in keys)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.SaveAsync(key, new MemoryStream([1]), Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.OpenReadAsync(key, Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.DeleteAsync(key, Ct));
        }

        Assert.False(File.Exists(Path.Combine(_sandbox, "fora.txt")));
        Assert.False(Directory.Exists(Path.Combine(_sandbox, "root-vizinha")));
    }

    [Fact]
    public async Task Fails_clearly_when_root_is_not_configured()
    {
        var unconfigured = new LocalDiskFileStorage(Options.Create(new StorageOptions()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unconfigured.SaveAsync($"{Guid.NewGuid()}/{Guid.NewGuid()}", new MemoryStream([1]), Ct));
        Assert.Contains("Storage:RootPath", error.Message, StringComparison.Ordinal);
    }

    private async Task<byte[]> ReadAsync(string key)
    {
        await using var stream = await _storage.OpenReadAsync(key, Ct);
        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }
}
