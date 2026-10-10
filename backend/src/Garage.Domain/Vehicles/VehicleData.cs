namespace Garage.Domain.Vehicles;

public sealed record VehicleData(
    VehicleType Type,
    string Make,
    string Model,
    string? Version,
    string? Generation,
    VehicleYear Year,
    Fuel Fuel,
    string? Nickname,
    Plate? Plate,
    string? Vin,
    string? Notes,
    CatalogRef? Catalog);
