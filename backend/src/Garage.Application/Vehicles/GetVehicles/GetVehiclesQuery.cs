using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Vehicles.GetVehicles;

public sealed record GetVehiclesQuery : IQuery<IReadOnlyList<VehicleSummaryDto>>;
