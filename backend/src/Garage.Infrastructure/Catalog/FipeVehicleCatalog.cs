using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Catalog;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;
using Garage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Garage.Infrastructure.Catalog;

/// <summary>
/// O catálogo sobre a FIPE (spec 6). Cada consulta tem a chave <c>fipe:{caminho}</c> no cache: dentro da validade responde
/// do cache; vencido, consulta a FIPE e renova; se a FIPE falha, serve o cache vencido (stale-on-error) e, sem cache,
/// responde <see cref="CatalogErrors.Unavailable"/>. Uma linha de cache que não se traduz mais conta como cache ausente.
/// </summary>
internal sealed partial class FipeVehicleCatalog(
    GarageDbContext db,
    FipeClient client,
    TimeProvider time,
    ILogger<FipeVehicleCatalog> logger) : IVehicleCatalog
{
    private static readonly TimeSpan ListValidity = TimeSpan.FromDays(30);
    private static readonly TimeSpan DetailValidity = TimeSpan.FromDays(7);

    public Task<Result<IReadOnlyList<CatalogMake>>> GetMakesAsync(VehicleType type, CancellationToken ct) =>
        GetAsync<IReadOnlyList<CatalogMake>>(
            $"{FipeType(type)}/brands",
            ListValidity,
            json => FipeJson.Items(json).Select(FipeTranslator.ToMake).ToList(),
            ct);

    public Task<Result<IReadOnlyList<CatalogModel>>> GetModelsAsync(VehicleType type, string makeId, CancellationToken ct) =>
        GetAsync<IReadOnlyList<CatalogModel>>(
            $"{FipeType(type)}/brands/{Segment(makeId)}/models",
            ListValidity,
            json => FipeJson.Items(json).Select(FipeTranslator.ToModel).ToList(),
            ct);

    public Task<Result<IReadOnlyList<CatalogYear>>> GetYearsAsync(
        VehicleType type, string makeId, string modelId, CancellationToken ct) =>
        GetAsync<IReadOnlyList<CatalogYear>>(
            $"{FipeType(type)}/brands/{Segment(makeId)}/models/{Segment(modelId)}/years",
            ListValidity,
            json => FipeJson.Items(json).Select(FipeTranslator.ToYear).ToList(),
            ct);

    public Task<Result<CatalogDetails>> GetDetailsAsync(
        VehicleType type, string makeId, string modelId, string yearId, CancellationToken ct) =>
        GetAsync(
            $"{FipeType(type)}/brands/{Segment(makeId)}/models/{Segment(modelId)}/years/{Segment(yearId)}",
            DetailValidity,
            json => FipeTranslator.ToDetails(FipeJson.Detail(json)),
            ct);

    public Task<Result<CatalogPrice>> GetPriceAsync(VehicleType type, string fipeCode, string yearId, CancellationToken ct) =>
        GetAsync(
            $"{FipeType(type)}/{Segment(fipeCode)}/years/{Segment(yearId)}",
            DetailValidity,
            json =>
            {
                var details = FipeTranslator.ToDetails(FipeJson.Detail(json));
                return new CatalogPrice(details.Price, details.ReferenceMonth);
            },
            ct);

    private async Task<Result<T>> GetAsync<T>(string path, TimeSpan validity, Func<string, T> translate, CancellationToken ct)
    {
        var key = $"fipe:{path}";
        var entry = await db.Set<CatalogCacheEntry>().AsNoTracking().FirstOrDefaultAsync(row => row.Key == key, ct);
        var now = time.GetUtcNow();

        // A linha guarda a resposta crua: se ela deixou de ser traduzível (contrato mudou, dado corrompido), vale como cache
        // ausente — consulta a FIPE de novo e, se ela falhar, responde catalog.unavailable em vez de um 500.
        var hasCache = TryTranslate(entry, path, translate, out var cached);
        if (hasCache && now - entry!.FetchedAt < validity)
            return cached!;

        string payload;
        T value;
        try
        {
            payload = await client.GetAsync(path, ct);
            // Traduz antes de guardar: uma resposta que não tem o formato esperado (página de erro de proxy, contrato
            // alterado) não pode ocupar o cache pela validade inteira.
            value = translate(payload);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
                                   || IsTranslationFailure(ex))
        {
            LogFipeFailed(logger, ex, path, hasStaleCache: hasCache);
            if (!hasCache)
                return CatalogErrors.Unavailable;

            return cached!;
        }

        await SaveAsync(key, payload, now, ct);
        return value;
    }

    private bool TryTranslate<T>(
        CatalogCacheEntry? entry, string path, Func<string, T> translate, [MaybeNullWhen(false)] out T value)
    {
        value = default;
        if (entry is null)
            return false;

        try
        {
            value = translate(entry.Payload);
            return true;
        }
        catch (Exception ex) when (IsTranslationFailure(ex))
        {
            LogCachedPayloadInvalid(logger, ex, path);
            return false;
        }
    }

    // Formato inesperado: JSON fora do contrato (JsonException) ou um valor que não é o que diz ser (FormatException).
    private static bool IsTranslationFailure(Exception exception) => exception is JsonException or FormatException;

    // Upsert em SQL: duas requisições para a mesma chave ao mesmo tempo não se atropelam (ON CONFLICT). Consultas não rodam
    // dentro de transação do UnitOfWork, então o comando é confirmado na hora, como o resto do cache.
    private Task SaveAsync(string key, string payload, DateTimeOffset fetchedAt, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO catalog_cache ("key", payload, fetched_at)
            VALUES ({key}, CAST({payload} AS jsonb), {fetchedAt})
            ON CONFLICT ("key") DO UPDATE SET payload = EXCLUDED.payload, fetched_at = EXCLUDED.fetched_at
            """,
            ct);

    private static string FipeType(VehicleType type) => type switch
    {
        VehicleType.Car => "cars",
        VehicleType.Motorcycle => "motorcycles",
        VehicleType.Truck => "trucks",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de veículo sem equivalente na FIPE.")
    };

    // Os códigos chegam do usuário (rota) e do vínculo guardado no veículo: nenhum pode alterar o caminho consultado.
    private static string Segment(string code) => Uri.EscapeDataString(code);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "FIPE request for {Path} failed (expired cache available: {HasStaleCache})")]
    private static partial void LogFipeFailed(ILogger logger, Exception exception, string path, bool hasStaleCache);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Cached FIPE payload for {Path} could not be translated; treating it as missing")]
    private static partial void LogCachedPayloadInvalid(ILogger logger, Exception exception, string path);
}
