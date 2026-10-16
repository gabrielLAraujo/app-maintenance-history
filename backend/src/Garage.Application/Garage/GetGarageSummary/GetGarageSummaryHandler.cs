using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Spending;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Garage.GetGarageSummary;

/// <summary>Resumo da garagem (spec 5.3): só os veículos do usuário (<c>OwnerId</c>), como <c>GET /vehicles</c>.</summary>
internal sealed class GetGarageSummaryHandler(
    ICurrentUser currentUser,
    IReadDbContext db,
    IAppClock clock) : IQueryHandler<GetGarageSummaryQuery, GarageSummaryDto>
{
    private const int RecentCount = 4;

    public async Task<Result<GarageSummaryDto>> Handle(GetGarageSummaryQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        // Mesma regra de Vehicle.DisplayName: o apelido, ou "marca modelo ano".
        var names = await db.Query<Vehicle>()
            .Where(v => v.OwnerId == userId)
            .Select(v => new { v.Id, v.Nickname, v.Make, v.Model, ModelYear = v.Year.Model })
            .ToDictionaryAsync(v => v.Id, v => v.Nickname ?? $"{v.Make} {v.Model} {v.ModelYear}", ct);

        // Uma única consulta de registros (com as peças, que são owned), agregada em memória (spec 5.5).
        var vehicleIds = names.Keys.ToList();
        var records = await db.Query<ServiceRecord>()
            .Where(r => vehicleIds.Contains(r.VehicleId))
            .ToListAsync(ct);

        var year = clock.CurrentYear;
        var ofYear = records.Where(r => r.Date.Year == year).ToList();

        var recent = records
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(RecentCount)
            .Select(r => new RecentRecordDto(r.Id, r.VehicleId, names[r.VehicleId], r.Title, r.Date, r.TotalCost.Amount))
            .ToList();

        return new GarageSummaryDto(
            year,
            SpendingMath.Total(ofYear),
            ofYear.Count,
            ofYear.Select(r => r.VehicleId).Distinct().Count(),
            SpendingMath.Months(ofYear, new DateOnly(year, 1, 1), 12),
            recent,
            records.Count,
            records.Count == 0 ? null : records.Min(r => r.Date));
    }
}
