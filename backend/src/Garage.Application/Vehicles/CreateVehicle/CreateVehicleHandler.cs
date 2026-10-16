using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Demo;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Vehicles.CreateVehicle;

internal sealed class CreateVehicleHandler(
    ICurrentUser currentUser,
    IReadDbContext db,
    IDemoLimits demoLimits,
    IVehicleRepository vehicles,
    IAppClock clock) : ICommandHandler<CreateVehicleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateVehicleCommand command, CancellationToken ct)
    {
        var ownerId = currentUser.UserId;

        // Spec 3.2: a conta demo tem um teto de veículos; as contas reais, não.
        if (currentUser.IsDemo
            && await db.Query<Vehicle>().CountAsync(vehicle => vehicle.OwnerId == ownerId, ct) >= demoLimits.MaxVehicles)
        {
            return DemoErrors.VehicleLimitReached(demoLimits.MaxVehicles);
        }

        var vehicle = Vehicle.Create(
            ownerId,
            command.Vehicle.ToData(clock.CurrentYear),
            new Odometer(command.CurrentOdometer),
            clock.Now);

        vehicles.Add(vehicle);
        return vehicle.Id;
    }
}
