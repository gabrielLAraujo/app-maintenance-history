using Garage.Application.Catalog;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Abstractions.Ports;

/// <summary>
/// Catálogo de veículos (hoje a FIPE). Todas as consultas falham com <see cref="CatalogErrors.Unavailable"/> quando o
/// catálogo não responde e não há nada guardado; cadastrar um veículo nunca depende dele.
/// </summary>
public interface IVehicleCatalog
{
    Task<Result<IReadOnlyList<CatalogMake>>> GetMakesAsync(VehicleType type, CancellationToken ct);

    Task<Result<IReadOnlyList<CatalogModel>>> GetModelsAsync(VehicleType type, string makeId, CancellationToken ct);

    Task<Result<IReadOnlyList<CatalogYear>>> GetYearsAsync(VehicleType type, string makeId, string modelId, CancellationToken ct);

    Task<Result<CatalogDetails>> GetDetailsAsync(
        VehicleType type, string makeId, string modelId, string yearId, CancellationToken ct);

    Task<Result<CatalogPrice>> GetPriceAsync(VehicleType type, string fipeCode, string yearId, CancellationToken ct);
}
