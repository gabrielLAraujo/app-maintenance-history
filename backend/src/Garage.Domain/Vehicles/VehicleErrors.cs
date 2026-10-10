using System.Globalization;
using Garage.Domain.Common;

namespace Garage.Domain.Vehicles;

public static class VehicleErrors
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static readonly Error NotFound = new(
        "vehicle.not_found",
        "Veículo não encontrado.",
        ErrorType.NotFound);

    public static readonly Error OdometerCannotDecrease = new(
        "vehicle.odometer_cannot_decrease",
        "A quilometragem não pode diminuir. Se digitou errado, use a correção de km.",
        ErrorType.DomainRule);

    public static readonly Error CatalogNotLinked = new(
        "vehicle.catalog_not_linked",
        "Este veículo não está vinculado à FIPE.",
        ErrorType.NotFound);

    public static Error OdometerBelowRecords(int maxRecordKm) => new(
        "vehicle.odometer_below_records",
        $"A km corrigida não pode ficar abaixo de {maxRecordKm.ToString("N0", PtBr)} km, a maior km do histórico.",
        ErrorType.DomainRule,
        new Dictionary<string, object?> { ["maxRecordKm"] = maxRecordKm });
}
