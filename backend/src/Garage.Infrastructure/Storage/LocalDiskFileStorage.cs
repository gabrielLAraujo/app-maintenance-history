using Garage.Application.Abstractions.Ports;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Storage;

/// <summary>
/// Arquivos dos anexos numa pasta local (<c>Storage:RootPath</c>, obrigatória: sem ela a API nem sobe). A raiz é lida a cada
/// operação, e a checagem de raiz vazia continua aqui como última barreira para quem usa a classe fora do host.
/// </summary>
internal sealed class LocalDiskFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private const int BufferSize = 81_920;

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Grava ao lado do destino (mesmo volume) e só então move: quem lê nunca encontra um arquivo pela metade.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var file = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(file, ct);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        try
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous);
            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // Sem a pasta do veículo também não há arquivo: nada a apagar.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Caminho absoluto da chave, que precisa ficar estritamente dentro da raiz: <c>..</c>, caminhos absolutos e a própria
    /// raiz são recusados. As chaves vêm do sistema; a checagem é a última barreira contra path traversal.
    /// </summary>
    private string ResolvePath(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var configured = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("Storage:RootPath não está configurado: defina a pasta dos anexos.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured)) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root, StringComparison.Ordinal) || path.Length == root.Length)
            throw new InvalidOperationException($"A chave de armazenamento '{key}' aponta para fora da pasta dos anexos.");

        return path;
    }
}
