using Garage.Api.Http;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Api.Catalog;

/// <summary>
/// O segmento <c>{type}</c> das rotas do catálogo usa os nossos valores (<c>car</c>, <c>motorcycle</c>, <c>truck</c>), nunca
/// os da FIPE. Lido como texto e convertido pelo nome, sem diferenciar maiúsculas; número ou nome desconhecido é inválido.
/// </summary>
internal static class CatalogTypeRoute
{
    public static bool TryParse(string value, out VehicleType type)
    {
        var name = Enum.GetNames<VehicleType>().FirstOrDefault(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
        type = name is null ? default : Enum.Parse<VehicleType>(name);
        return name is not null;
    }

    /// <summary>400 <c>validation.failed</c> com o erro no campo <c>type</c>.</summary>
    public static IResult Invalid() =>
        Error.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["type"] = ["Tipo de veículo inválido. Use car, motorcycle ou truck."]
        }).ToProblem();
}
