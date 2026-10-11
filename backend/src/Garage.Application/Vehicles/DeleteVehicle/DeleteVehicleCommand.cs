using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Vehicles.DeleteVehicle;

public sealed record DeleteVehicleCommand(Guid VehicleId) : ICommand<Unit>;
