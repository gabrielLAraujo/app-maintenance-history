using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Sharing;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Reminders;
using Garage.Domain.Sharing;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Demo.SeedDemoData;

internal sealed class SeedDemoDataHandler(
    ICurrentUser currentUser,
    IReadDbContext db,
    IVehicleRepository vehicles,
    IServiceRecordRepository records,
    IReminderRepository reminders,
    IShareLinkRepository shareLinks,
    IAppClock clock) : ICommandHandler<SeedDemoDataCommand, DemoSeedResult>
{
    public async Task<Result<DemoSeedResult>> Handle(SeedDemoDataCommand command, CancellationToken ct)
    {
        var ownerId = currentUser.UserId;
        if (await db.Query<Vehicle>().AnyAsync(vehicle => vehicle.OwnerId == ownerId, ct))
            return DemoErrors.AlreadyHasData;

        var today = clock.Today;
        var now = clock.Now;
        var demoVehicles = DemoDataCatalog.Build(today);

        // Nada é gravado aqui: o TransactionCommandDecorator faz um único SaveChanges, então uma falha em qualquer
        // item (que o catálogo não deve ter) descarta tudo, sem deixar metade dos dados de exemplo.
        foreach (var demo in demoVehicles)
        {
            var vehicle = Vehicle.Create(ownerId, demo.Data, demo.CurrentOdometer, now);
            vehicles.Add(vehicle);

            foreach (var details in demo.Records)
            {
                var created = ServiceRecord.Create(vehicle.Id, details, BoundsFor(details, demo.Records), [], today, now);
                if (created.IsFailure)
                    return created.Error!;

                records.Add(created.Value);
            }

            foreach (var item in demo.Reminders)
            {
                var reminder = Reminder.Create(
                    vehicle.Id, item.Title, item.Interval, item.LastDoneDate, item.LastDoneOdometer, today);
                if (reminder.IsFailure)
                    return reminder.Error!;

                reminders.Add(reminder.Value);
            }

            if (demo.PublicLink is { } link)
            {
                shareLinks.Add(ShareLink.Create(
                    vehicle.Id, ShareTokens.New(), link.Label, link.IncludeCosts, link.IncludePlate, expiresAt: null, now));
            }
        }

        return new DemoSeedResult(
            demoVehicles.Count,
            demoVehicles.Sum(demo => demo.Records.Count),
            demoVehicles.Sum(demo => demo.Reminders.Count));
    }

    /// <summary>
    /// R3 sem consultar o banco (nada foi gravado ainda): a maior km entre os registros mais antigos e a menor entre os mais
    /// novos, como <see cref="IServiceRecordRepository.GetOdometerBoundsAsync"/> calcularia.
    /// </summary>
    private static OdometerBounds BoundsFor(RecordDetails details, IReadOnlyList<RecordDetails> all) => new(
        all.Where(other => other.Date < details.Date).Select(other => other.Odometer?.Km).Max(),
        all.Where(other => other.Date > details.Date).Select(other => other.Odometer?.Km).Min());
}
