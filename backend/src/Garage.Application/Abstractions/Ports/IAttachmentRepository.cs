using Garage.Domain.Attachments;

namespace Garage.Application.Abstractions.Ports;

/// <summary>
/// Repositório do agregado <see cref="Attachment"/> para os comandos (as consultas usam <see cref="IReadDbContext"/>).
/// Os anexos são sempre buscados pelo veículo <b>e</b> pelo id; não verificam acesso: use <see cref="IVehicleAccess"/> antes.
/// </summary>
public interface IAttachmentRepository
{
    /// <summary>Carrega o anexo do veículo para alteração (rastreado); <c>null</c> se não existir nele.</summary>
    Task<Attachment?> GetAsync(Guid vehicleId, Guid attachmentId, CancellationToken ct);

    /// <summary>Quantos anexos o registro já tem (A3).</summary>
    Task<int> CountByRecordAsync(Guid recordId, CancellationToken ct);

    /// <summary>As chaves dos arquivos de todos os anexos do veículo, para apagá-los do disco depois do commit.</summary>
    Task<IReadOnlyList<string>> GetStorageKeysByVehicleAsync(Guid vehicleId, CancellationToken ct);

    /// <summary>As chaves dos arquivos dos anexos do registro, para apagá-los do disco depois do commit.</summary>
    Task<IReadOnlyList<string>> GetStorageKeysByRecordAsync(Guid recordId, CancellationToken ct);

    void Add(Attachment a);

    void Remove(Attachment a);
}
