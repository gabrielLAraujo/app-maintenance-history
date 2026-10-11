using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;

namespace Garage.Application.Records.DeleteServiceRecord;

internal sealed class DeleteServiceRecordHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IServiceRecordRepository records,
    IAttachmentRepository attachments,
    IFileStorage storage,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteServiceRecordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteServiceRecordCommand command, CancellationToken ct)
    {
        if (!await access.CanEditAsync(command.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var record = await records.GetAsync(command.VehicleId, command.RecordId, ct);
        if (record is null)
            return ServiceRecordErrors.NotFound;

        // As linhas dos anexos saem em cascata com o registro; os arquivos, só depois do commit (spec 5.5). As chaves são
        // lidas antes, enquanto as linhas existem. Uma ação por arquivo: a falha de um não impede apagar os outros.
        foreach (var key in await attachments.GetStorageKeysByRecordAsync(record.Id, ct))
            unitOfWork.AfterCommit(afterCt => storage.DeleteAsync(key, afterCt));

        // V4: a km atual do veículo fica como está (para corrigir, CorrectOdometer). R6: lembretes não são tocados.
        records.Remove(record);
        return Unit.Value;
    }
}
