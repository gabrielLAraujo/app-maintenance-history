using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles;

internal static class VehicleInputMapping
{
    /// <summary>
    /// Converte a entrada, que o validator já aprovou, nos dados do domínio. Os textos são aparados e os opcionais em
    /// branco viram nulos (um apelido "" ocuparia o lugar do nome de exibição).
    /// </summary>
    public static VehicleData ToData(this VehicleInput input, int currentYear) => new(
        input.Type,
        input.Make.Trim(),
        input.Model.Trim(),
        Clean(input.Version),
        Clean(input.Generation),
        VehicleYear.From(input.ManufactureYear, input.ModelYear, currentYear),
        input.Fuel,
        Clean(input.Nickname),
        Clean(input.Plate) is { } plate ? Plate.From(plate) : null,
        Clean(input.Vin),
        Clean(input.Notes),
        input.Catalog is { } catalog
            ? new CatalogRef(catalog.Type, catalog.FipeCode.Trim(), catalog.FipeYearCode.Trim())
            : null);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
