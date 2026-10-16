using Garage.Application.Spending;

namespace Garage.Application.Garage;

/// <summary>Um registro recente de qualquer veículo do usuário, para o painel.</summary>
public sealed record RecentRecordDto(Guid Id, Guid VehicleId, string VehicleName, string Title, DateOnly Date, decimal TotalCost);

/// <summary>
/// Resumo da garagem (spec 5.3): gasto, registros e veículos com registro no ano corrente (fuso <c>App:TimeZone</c>),
/// janeiro a dezembro desse ano, os 4 registros mais recentes e os totais de todo o histórico.
/// </summary>
public sealed record GarageSummaryDto(
    int Year,
    decimal YearCost,
    int YearRecordCount,
    int YearVehicleCount,
    IReadOnlyList<MonthCostDto> ByMonth,
    IReadOnlyList<RecentRecordDto> RecentRecords,
    int RecordCount,
    DateOnly? FirstRecordDate);
