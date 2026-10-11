using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Records.AddServiceRecord;

public sealed record AddServiceRecordCommand(Guid VehicleId, RecordInput Record, IReadOnlyList<Guid> FulfilledReminderIds)
    : ICommand<Guid>;
