using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Vehicles;

namespace Garage.Application.Catalog.GetCatalogModels;

public sealed record GetCatalogModelsQuery(VehicleType Type, string MakeId) : IQuery<IReadOnlyList<CatalogModel>>;
