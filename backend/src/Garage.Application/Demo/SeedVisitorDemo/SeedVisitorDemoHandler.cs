using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Demo.SeedVisitorDemo;

internal sealed class SeedVisitorDemoHandler(DemoDataSeeder seeder) : ICommandHandler<SeedVisitorDemoCommand, DemoSeedResult>
{
    public Task<Result<DemoSeedResult>> Handle(SeedVisitorDemoCommand command, CancellationToken ct) =>
        seeder.SeedAsync(command.OwnerId, ct);
}
