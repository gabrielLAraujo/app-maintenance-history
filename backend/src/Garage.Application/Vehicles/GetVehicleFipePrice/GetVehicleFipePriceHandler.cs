using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Catalog;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Vehicles.GetVehicleFipePrice;

internal sealed class GetVehicleFipePriceHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db,
    IVehicleCatalog catalog) : IQueryHandler<GetVehicleFipePriceQuery, CatalogPrice>
{
    public async Task<Result<CatalogPrice>> Handle(GetVehicleFipePriceQuery query, CancellationToken ct)
    {
        if (!await access.CanReadAsync(query.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        // Linha nula: o veículo sumiu entre o acesso e a leitura. Catalog nulo: nunca foi vinculado à FIPE.
        var vehicle = await db.Query<Vehicle>()
            .Where(v => v.Id == query.VehicleId)
            .Select(v => new
            {
                Catalog = v.Catalog == null ? null : new CatalogRefInput(v.Catalog.Type, v.Catalog.FipeCode, v.Catalog.FipeYearCode)
            })
            .FirstOrDefaultAsync(ct);

        if (vehicle is null)
            return VehicleErrors.NotFound;

        if (vehicle.Catalog is null)
            return VehicleErrors.CatalogNotLinked;

        return await catalog.GetPriceAsync(vehicle.Catalog.Type, vehicle.Catalog.FipeCode, vehicle.Catalog.FipeYearCode, ct);
    }
}
