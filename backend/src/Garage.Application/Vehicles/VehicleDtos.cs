using Garage.Application.Spending;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles;

public sealed record CatalogRefInput(VehicleType Type, string FipeCode, string FipeYearCode);

public sealed record VehicleInput(
    VehicleType Type,
    string Make,
    string Model,
    string? Version,
    string? Generation,
    int ManufactureYear,
    int ModelYear,
    Fuel Fuel,
    string? Nickname,
    string? Plate,
    string? Vin,
    string? Notes,
    CatalogRefInput? Catalog);

public sealed record VehicleSummaryDto(
    Guid Id,
    string DisplayName,
    string Make,
    string Model,
    int ModelYear,
    string? Plate,
    int CurrentOdometer,
    Guid? CoverPhotoId,
    decimal TotalCost,
    DateOnly? LastRecordDate);

public sealed record VehicleDto(
    Guid Id,
    VehicleType Type,
    string Make,
    string Model,
    string? Version,
    string? Generation,
    int ManufactureYear,
    int ModelYear,
    Fuel Fuel,
    string? Nickname,
    string? Plate,
    string? Vin,
    string? Notes,
    int CurrentOdometer,
    DateTimeOffset OdometerUpdatedAt,
    CatalogRefInput? Catalog,
    Guid? CoverPhotoId,
    uint RowVersion);

/// <summary>Gasto e quantidade de registros de um tipo (manutenção ou modificação).</summary>
public sealed record TypeSpendingDto(decimal Cost, int Count);

/// <summary>Referência a um registro do histórico.</summary>
public sealed record RecordRefDto(Guid Id, string Title, DateOnly Date);

/// <summary>
/// Resumo de gastos do veículo (spec 5.2). <see cref="KmTracked"/> é a km atual menos a menor km entre os registros;
/// <see cref="CostPerKm"/> só com <see cref="KmTracked"/> ≥ 1.000. <see cref="ByMonth"/> traz os últimos 12 meses.
/// </summary>
public sealed record VehicleSpendingDto(
    decimal TotalCost,
    TypeSpendingDto Maintenance,
    TypeSpendingDto Modification,
    DateOnly? FirstRecordDate,
    int? KmTracked,
    decimal? CostPerKm,
    IReadOnlyList<CategoryCostDto> ByCategory,
    IReadOnlyList<MonthCostDto> ByMonth,
    RecordRefDto? LastRecord);
