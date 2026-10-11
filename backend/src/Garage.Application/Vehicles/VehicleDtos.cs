using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles;

public sealed record CatalogRefInput(VehicleType Type, string FipeCode, string FipeYearCode);

public sealed record VehicleInput(
    VehicleType Type,
    string Make,
    string Model,
    string? Version,
    string? Generation,
    int ManufactureYear,
    int ModelYear,
    Fuel Fuel,
    string? Nickname,
    string? Plate,
    string? Vin,
    string? Notes,
    CatalogRefInput? Catalog);

public sealed record VehicleSummaryDto(
    Guid Id,
    string DisplayName,
    string Make,
    string Model,
    int ModelYear,
    string? Plate,
    int CurrentOdometer);

public sealed record VehicleDto(
    Guid Id,
    VehicleType Type,
    string Make,
    string Model,
    string? Version,
    string? Generation,
    int ManufactureYear,
    int ModelYear,
    Fuel Fuel,
    string? Nickname,
    string? Plate,
    string? Vin,
    string? Notes,
    int CurrentOdometer,
    DateTimeOffset OdometerUpdatedAt,
    CatalogRefInput? Catalog,
    uint RowVersion);
