using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Vehicles.UpdateVehicle;

public sealed record UpdateVehicleCommand(Guid VehicleId, VehicleInput Vehicle, uint RowVersion) : ICommand<Unit>;
