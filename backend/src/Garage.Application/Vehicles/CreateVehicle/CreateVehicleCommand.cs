using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Vehicles.CreateVehicle;

public sealed record CreateVehicleCommand(VehicleInput Vehicle, int CurrentOdometer) : ICommand<Guid>;
