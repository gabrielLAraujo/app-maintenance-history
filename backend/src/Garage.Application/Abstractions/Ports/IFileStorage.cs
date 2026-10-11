namespace Garage.Application.Abstractions.Ports;

/// <summary>
/// Arquivos dos anexos. As chaves são geradas pelo sistema (<c>{vehicleId}/{attachmentId}</c>, nunca o nome enviado);
/// uma chave que resolva fora da raiz do armazenamento é recusada com <see cref="InvalidOperationException"/>.
/// </summary>
public interface IFileStorage
{
    /// <summary>Grava o arquivo da chave por inteiro (substitui se já existir): quem lê nunca vê um arquivo pela metade.</summary>
    Task SaveAsync(string key, Stream content, CancellationToken ct);

    /// <summary>Abre o arquivo para leitura; <c>null</c> se não existir. Quem chama descarta o stream.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    /// <summary>Apaga o arquivo; não faz nada se ele não existir.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}
