using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Vehicles.GetVehicles;

internal sealed class GetVehiclesHandler(
    ICurrentUser currentUser,
    IReadDbContext db) : IQueryHandler<GetVehiclesQuery, IReadOnlyList<VehicleSummaryDto>>
{
    public async Task<Result<IReadOnlyList<VehicleSummaryDto>>> Handle(GetVehiclesQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        // O nome de exibição não é coluna: é calculado em memória, pela mesma regra de Vehicle.DisplayName
        // (apelido, ou "marca modelo ano"); Plate e Odometer são value objects convertidos pelo EF.
        var rows = await db.Query<Vehicle>()
            .Where(v => v.OwnerId == userId)
            .OrderBy(v => v.Id)
            .Select(v => new
            {
                v.Id,
                v.Nickname,
                v.Make,
                v.Model,
                ModelYear = v.Year.Model,
                v.Plate,
                v.CurrentOdometer
            })
            .ToListAsync(ct);

        IReadOnlyList<VehicleSummaryDto> vehicles = rows
            .Select(row => new VehicleSummaryDto(
                row.Id,
                row.Nickname ?? $"{row.Make} {row.Model} {row.ModelYear}",
                row.Make,
                row.Model,
                row.ModelYear,
                row.Plate?.Value,
                row.CurrentOdometer.Km))
            .ToList();
        return Result<IReadOnlyList<VehicleSummaryDto>>.Success(vehicles);
    }
}
