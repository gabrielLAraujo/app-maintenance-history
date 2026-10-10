using Garage.Domain.Common;

namespace Garage.Domain.Records;

public sealed record ServiceRecordAdded(
    Guid RecordId,
    Guid VehicleId,
    DateOnly Date,
    Odometer? Odometer,
    IReadOnlyList<Guid> FulfilledReminderIds) : IDomainEvent;
