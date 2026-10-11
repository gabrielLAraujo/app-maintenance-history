using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles.CreateVehicle;

internal sealed class CreateVehicleHandler(
    ICurrentUser currentUser,
    IVehicleRepository vehicles,
    IAppClock clock) : ICommandHandler<CreateVehicleCommand, Guid>
{
    public Task<Result<Guid>> Handle(CreateVehicleCommand command, CancellationToken ct)
    {
        var vehicle = Vehicle.Create(
            currentUser.UserId,
            command.Vehicle.ToData(clock.CurrentYear),
            new Odometer(command.CurrentOdometer),
            clock.Now);

        vehicles.Add(vehicle);
        return Task.FromResult<Result<Guid>>(vehicle.Id);
    }
}
