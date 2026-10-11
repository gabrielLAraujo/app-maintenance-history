using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Records.GetServiceRecord;

public sealed record GetServiceRecordQuery(Guid VehicleId, Guid RecordId) : IQuery<RecordDto>;
