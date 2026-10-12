using Garage.Application.Abstractions.Messaging;
using Garage.Application.Catalog;

namespace Garage.Application.Vehicles.GetVehicleFipePrice;

/// <summary>O preço atual do veículo na FIPE, pelo vínculo (<c>Catalog</c>) guardado nele.</summary>
public sealed record GetVehicleFipePriceQuery(Guid VehicleId) : IQuery<CatalogPrice>;
