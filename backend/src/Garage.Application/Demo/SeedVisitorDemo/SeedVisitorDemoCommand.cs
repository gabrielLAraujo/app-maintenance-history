using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Demo.SeedVisitorDemo;

/// <summary>Os dados de exemplo para a conta demo recém-criada (<paramref name="OwnerId"/>), ainda sem sessão.</summary>
public sealed record SeedVisitorDemoCommand(Guid OwnerId) : ICommand<DemoSeedResult>;
