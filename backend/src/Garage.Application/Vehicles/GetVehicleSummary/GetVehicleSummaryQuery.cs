using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Vehicles.GetVehicleSummary;

public sealed record GetVehicleSummaryQuery(Guid VehicleId) : IQuery<VehicleSpendingDto>;
