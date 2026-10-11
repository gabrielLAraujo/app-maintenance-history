using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Vehicles.GetVehicle;

public sealed record GetVehicleQuery(Guid VehicleId) : IQuery<VehicleDto>;
