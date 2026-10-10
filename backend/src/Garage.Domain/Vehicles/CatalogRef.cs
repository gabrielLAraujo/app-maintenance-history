namespace Garage.Domain.Vehicles;

public sealed record CatalogRef(VehicleType Type, string FipeCode, string FipeYearCode);
