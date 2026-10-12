using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Vehicles;

namespace Garage.Application.Catalog.GetCatalogYears;

public sealed record GetCatalogYearsQuery(VehicleType Type, string MakeId, string ModelId) : IQuery<IReadOnlyList<CatalogYear>>;
