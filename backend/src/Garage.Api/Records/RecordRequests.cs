using Garage.Application.Records;
using Garage.Application.Records.AddServiceRecord;
using Garage.Application.Records.UpdateServiceRecord;
using Garage.Domain.Records;

namespace Garage.Api.Records;

// Os corpos são planos (os campos do registro no nível raiz), então as chaves de validation.failed
// batem com os campos do formulário; cada request monta o comando com um RecordInput.
// Peças e lembretes cumpridos podem faltar no corpo: valem como listas vazias.

public sealed record AddRecordRequest(
    RecordType Type,
    DateOnly Date,
    int? Odometer,
    string Title,
    string? Description,
    Category Category,
    string? PerformedBy,
    decimal? LaborCost,
    string? ReplacedOriginalPart,
    IReadOnlyList<PartInput>? Parts,
    IReadOnlyList<Guid>? FulfilledReminderIds)
{
    internal AddServiceRecordCommand ToCommand(Guid vehicleId) => new(
        vehicleId,
        new RecordInput(
            Type, Date, Odometer, Title, Description, Category, PerformedBy, LaborCost, ReplacedOriginalPart, Parts ?? []),
        FulfilledReminderIds ?? []);
}

public sealed record UpdateRecordRequest(
    RecordType Type,
    DateOnly Date,
    int? Odometer,
    string Title,
    string? Description,
    Category Category,
    string? PerformedBy,
    decimal? LaborCost,
    string? ReplacedOriginalPart,
    IReadOnlyList<PartInput>? Parts,
    uint RowVersion)
{
    internal UpdateServiceRecordCommand ToCommand(Guid vehicleId, Guid recordId) => new(
        vehicleId,
        recordId,
        new RecordInput(
            Type, Date, Odometer, Title, Description, Category, PerformedBy, LaborCost, ReplacedOriginalPart, Parts ?? []),
        RowVersion);
}
