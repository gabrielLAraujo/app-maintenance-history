using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Records.DeleteServiceRecord;

public sealed record DeleteServiceRecordCommand(Guid VehicleId, Guid RecordId) : ICommand<Unit>;
