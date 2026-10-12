using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Vehicles;

namespace Garage.Application.Catalog.GetCatalogMakes;

public sealed record GetCatalogMakesQuery(VehicleType Type) : IQuery<IReadOnlyList<CatalogMake>>;
