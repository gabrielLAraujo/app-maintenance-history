using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Vehicles;

namespace Garage.Application.Catalog.GetCatalogDetails;

public sealed record GetCatalogDetailsQuery(VehicleType Type, string MakeId, string ModelId, string YearId)
    : IQuery<CatalogDetails>;
