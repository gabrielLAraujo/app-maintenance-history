using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Records.UpdateServiceRecord;

public sealed record UpdateServiceRecordCommand(Guid VehicleId, Guid RecordId, RecordInput Record, uint RowVersion)
    : ICommand<Unit>;
