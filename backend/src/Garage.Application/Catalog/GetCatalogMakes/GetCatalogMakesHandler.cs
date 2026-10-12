using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;

namespace Garage.Application.Catalog.GetCatalogMakes;

internal sealed class GetCatalogMakesHandler(IVehicleCatalog catalog)
    : IQueryHandler<GetCatalogMakesQuery, IReadOnlyList<CatalogMake>>
{
    public Task<Result<IReadOnlyList<CatalogMake>>> Handle(GetCatalogMakesQuery query, CancellationToken ct) =>
        catalog.GetMakesAsync(query.Type, ct);
}
