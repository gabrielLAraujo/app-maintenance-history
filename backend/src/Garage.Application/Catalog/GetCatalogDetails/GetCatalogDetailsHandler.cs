using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;

namespace Garage.Application.Catalog.GetCatalogDetails;

internal sealed class GetCatalogDetailsHandler(IVehicleCatalog catalog) : IQueryHandler<GetCatalogDetailsQuery, CatalogDetails>
{
    public Task<Result<CatalogDetails>> Handle(GetCatalogDetailsQuery query, CancellationToken ct) =>
        catalog.GetDetailsAsync(query.Type, query.MakeId, query.ModelId, query.YearId, ct);
}
