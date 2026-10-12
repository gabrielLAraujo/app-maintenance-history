using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Demo.SeedDemoData;

/// <summary>
/// Cria os veículos de exemplo (<see cref="DemoDataCatalog"/>) na conta de quem chama. Só vale para conta sem veículos:
/// senão devolve <see cref="DemoErrors.AlreadyHasData"/>.
/// </summary>
public sealed record SeedDemoDataCommand : ICommand<DemoSeedResult>;
