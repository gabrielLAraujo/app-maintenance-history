using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Vehicles.CorrectOdometer;

public sealed record CorrectOdometerCommand(Guid VehicleId, int Km) : ICommand<Unit>;
