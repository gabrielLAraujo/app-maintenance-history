using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Vehicles.GetVehicle;

internal sealed class GetVehicleHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db) : IQueryHandler<GetVehicleQuery, VehicleDto>
{
    public async Task<Result<VehicleDto>> Handle(GetVehicleQuery query, CancellationToken ct)
    {
        if (!await access.CanReadAsync(query.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await db.Query<Vehicle>()
            .Where(v => v.Id == query.VehicleId)
            .Select(v => new VehicleDto(
                v.Id,
                v.Type,
                v.Make,
                v.Model,
                v.Version,
                v.Generation,
                v.Year.Manufacture,
                v.Year.Model,
                v.Fuel,
                v.Nickname,
                v.Plate == null ? null : v.Plate.Value,
                v.Vin,
                v.Notes,
                v.CurrentOdometer.Km,
                v.OdometerUpdatedAt,
                v.Catalog == null ? null : new CatalogRefInput(v.Catalog.Type, v.Catalog.FipeCode, v.Catalog.FipeYearCode),
                EF.Property<uint>(v, "RowVersion")))
            .FirstOrDefaultAsync(ct);

        return vehicle is null ? VehicleErrors.NotFound : vehicle;
    }
}
