using System.Text.Json;

namespace Garage.Infrastructure.Catalog;

// O formato da FIPE (v2). Só o FipeTranslator os conhece; o resto do sistema vê os tipos de Garage.Application.Catalog.

/// <summary>Uma marca, um modelo ou um ano: <c>code</c> e <c>name</c>.</summary>
internal sealed record FipeItem(string Code, string Name);

/// <summary>
/// A cotação de um veículo/ano. <c>price</c> é texto ("R$ 10.000,00") e <c>referenceMonth</c> também. <c>vehicleType</c> e
/// <c>fuelAcronym</c> não entram na tradução: são opcionais, para a falta deles não derrubar a cotação.
/// </summary>
internal sealed record FipeVehicleDetail(
    string Price,
    string Brand,
    string Model,
    int ModelYear,
    string Fuel,
    string CodeFipe,
    string ReferenceMonth,
    int? VehicleType = null,
    string? FuelAcronym = null);

/// <summary>
/// Leitura estrita do JSON da FIPE: campo usado pelo tradutor ausente ou nulo é <see cref="JsonException"/>. Uma resposta que
/// não tem o formato esperado (página de erro de um proxy, contrato alterado) não pode ser traduzida nem guardada no cache.
/// </summary>
internal static class FipeJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public static IReadOnlyList<FipeItem> Items(string json) =>
        JsonSerializer.Deserialize<List<FipeItem>>(json, Options)
        ?? throw new JsonException("A FIPE respondeu null no lugar de uma lista.");

    public static FipeVehicleDetail Detail(string json) =>
        JsonSerializer.Deserialize<FipeVehicleDetail>(json, Options)
        ?? throw new JsonException("A FIPE respondeu null no lugar de uma cotação.");
}
