using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Spending;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Vehicles.GetVehicleSummary;

/// <summary>Resumo de gastos do veículo (spec 5.2), agregado em memória sobre os registros com peças.</summary>
internal sealed class GetVehicleSummaryHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db,
    IAppClock clock) : IQueryHandler<GetVehicleSummaryQuery, VehicleSpendingDto>
{
    /// <summary>Últimos 12 meses, incluindo o atual.</summary>
    private const int MonthCount = 12;

    /// <summary>Abaixo disso o custo por km seria enganoso (spec 5.1).</summary>
    private const int MinKmForCostPerKm = 1_000;

    public async Task<Result<VehicleSpendingDto>> Handle(GetVehicleSummaryQuery query, CancellationToken ct)
    {
        if (!await access.CanReadAsync(query.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var vehicle = await db.Query<Vehicle>()
            .Where(v => v.Id == query.VehicleId)
            .Select(v => new { v.CurrentOdometer })
            .FirstOrDefaultAsync(ct);
        if (vehicle is null)
            return VehicleErrors.NotFound;

        var records = await db.Query<ServiceRecord>()
            .Where(r => r.VehicleId == query.VehicleId)
            .ToListAsync(ct);

        var total = SpendingMath.Total(records);
        var minKm = records.Select(r => r.Odometer?.Km).Min();
        int? kmTracked = minKm is { } km ? Math.Max(0, vehicle.CurrentOdometer.Km - km) : null;
        decimal? costPerKm = kmTracked >= MinKmForCostPerKm
            ? Math.Round(total / kmTracked.Value, 2, MidpointRounding.AwayFromZero)
            : null;
        var latest = SpendingMath.Latest(records);
        var thisMonth = clock.Today;

        return new VehicleSpendingDto(
            total,
            Spending(records, RecordType.Maintenance),
            Spending(records, RecordType.Modification),
            records.Count == 0 ? null : records.Min(r => r.Date),
            kmTracked,
            costPerKm,
            SpendingMath.ByCategory(records),
            SpendingMath.Months(records, thisMonth.AddMonths(-(MonthCount - 1)), MonthCount),
            latest is null ? null : new RecordRefDto(latest.Id, latest.Title, latest.Date));
    }

    private static TypeSpendingDto Spending(IEnumerable<ServiceRecord> records, RecordType type)
    {
        var ofType = records.Where(r => r.Type == type).ToList();
        return new TypeSpendingDto(SpendingMath.Total(ofType), ofType.Count);
    }
}
