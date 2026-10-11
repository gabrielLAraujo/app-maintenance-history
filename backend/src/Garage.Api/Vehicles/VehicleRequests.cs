using Garage.Application.Vehicles;
using Garage.Application.Vehicles.CorrectOdometer;
using Garage.Application.Vehicles.CreateVehicle;
using Garage.Application.Vehicles.UpdateOdometer;
using Garage.Application.Vehicles.UpdateVehicle;
using Garage.Domain.Vehicles;

namespace Garage.Api.Vehicles;

// Os corpos são planos (os campos do veículo no nível raiz), então as chaves de validation.failed
// batem com os campos do formulário; cada request monta o comando com um VehicleInput.

public sealed record CreateVehicleRequest(
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
    CatalogRefInput? Catalog,
    int CurrentOdometer)
{
    internal CreateVehicleCommand ToCommand() => new(
        new VehicleInput(
            Type, Make, Model, Version, Generation, ManufactureYear, ModelYear, Fuel, Nickname, Plate, Vin, Notes, Catalog),
        CurrentOdometer);
}

public sealed record UpdateVehicleRequest(
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
    CatalogRefInput? Catalog,
    uint RowVersion)
{
    internal UpdateVehicleCommand ToCommand(Guid vehicleId) => new(
        vehicleId,
        new VehicleInput(
            Type, Make, Model, Version, Generation, ManufactureYear, ModelYear, Fuel, Nickname, Plate, Vin, Notes, Catalog),
        RowVersion);
}

public sealed record UpdateOdometerRequest(int Km)
{
    internal UpdateOdometerCommand ToCommand(Guid vehicleId) => new(vehicleId, Km);
}

public sealed record CorrectOdometerRequest(int Km)
{
    internal CorrectOdometerCommand ToCommand(Guid vehicleId) => new(vehicleId, Km);
}
