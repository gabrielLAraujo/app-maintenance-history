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
    IUnitOfWork unitOfWork,
    IVehicleRepository vehicles,
    IAppClock clock) : ICommandHandler<CreateVehicleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateVehicleCommand command, CancellationToken ct)
    {
        var ownerId = currentUser.UserId;

        // Spec 3.2: a conta demo tem um teto de veículos; as contas reais, não. Contar e gravar acontecem sob uma trava da
        // conta, solta só depois do commit (ou do descarte): pedidos simultâneos não furam o teto.
        if (currentUser.IsDemo)
        {
            var gate = await demoLimits.EnterVehicleGateAsync(ownerId, ct);
            unitOfWork.AfterCommit(_ => Release(gate));
            unitOfWork.OnRollback(_ => Release(gate));

            if (await db.Query<Vehicle>().CountAsync(vehicle => vehicle.OwnerId == ownerId, ct) >= demoLimits.MaxVehicles)
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

    private static Task Release(IDisposable gate)
    {
        gate.Dispose();
        return Task.CompletedTask;
    }
}
