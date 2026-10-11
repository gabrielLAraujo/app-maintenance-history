using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Vehicles.UpdateOdometer;

public sealed record UpdateOdometerCommand(Guid VehicleId, int Km) : ICommand<Unit>;
