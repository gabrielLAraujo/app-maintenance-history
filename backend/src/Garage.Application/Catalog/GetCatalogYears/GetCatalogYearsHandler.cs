using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;

namespace Garage.Application.Catalog.GetCatalogYears;

internal sealed class GetCatalogYearsHandler(IVehicleCatalog catalog)
    : IQueryHandler<GetCatalogYearsQuery, IReadOnlyList<CatalogYear>>
{
    public Task<Result<IReadOnlyList<CatalogYear>>> Handle(GetCatalogYearsQuery query, CancellationToken ct) =>
        catalog.GetYearsAsync(query.Type, query.MakeId, query.ModelId, ct);
}
