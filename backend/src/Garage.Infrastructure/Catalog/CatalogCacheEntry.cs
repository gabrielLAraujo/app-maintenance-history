namespace Garage.Infrastructure.Catalog;

/// <summary>
/// Uma resposta da FIPE guardada como veio (JSON). A chave é <c>fipe:{caminho}</c>. Só é lida pela entidade; a gravação é um
/// upsert em SQL (ver <see cref="FipeVehicleCatalog"/>).
/// </summary>
internal sealed class CatalogCacheEntry
{
    public required string Key { get; set; }

    public required string Payload { get; set; }

    public required DateTimeOffset FetchedAt { get; set; }
}
