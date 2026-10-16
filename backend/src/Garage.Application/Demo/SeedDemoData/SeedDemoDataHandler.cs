using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Demo.SeedDemoData;

internal sealed class SeedDemoDataHandler(
    ICurrentUser currentUser,
    IReadDbContext db,
    DemoDataSeeder seeder) : ICommandHandler<SeedDemoDataCommand, DemoSeedResult>
{
    public async Task<Result<DemoSeedResult>> Handle(SeedDemoDataCommand command, CancellationToken ct)
    {
        var ownerId = currentUser.UserId;
        if (await db.Query<Vehicle>().AnyAsync(vehicle => vehicle.OwnerId == ownerId, ct))
            return DemoErrors.AlreadyHasData;

        return await seeder.SeedAsync(ownerId, ct);
    }
}
