using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;

namespace Garage.Application.Catalog.GetCatalogModels;

internal sealed class GetCatalogModelsHandler(IVehicleCatalog catalog)
    : IQueryHandler<GetCatalogModelsQuery, IReadOnlyList<CatalogModel>>
{
    public Task<Result<IReadOnlyList<CatalogModel>>> Handle(GetCatalogModelsQuery query, CancellationToken ct) =>
        catalog.GetModelsAsync(query.Type, query.MakeId, ct);
}
