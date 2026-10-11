using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;

namespace Garage.Application.Records.DeleteServiceRecord;

internal sealed class DeleteServiceRecordHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IServiceRecordRepository records) : ICommandHandler<DeleteServiceRecordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteServiceRecordCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var record = await records.GetAsync(command.VehicleId, command.RecordId, ct);
        if (record is null)
            return ServiceRecordErrors.NotFound;

        // V4: a km atual do veículo fica como está (para corrigir, CorrectOdometer). R6: lembretes não são tocados.
        records.Remove(record);
        return Unit.Value;
    }
}
