using Garage.Application.Attachments;
using Garage.Domain.Records;

namespace Garage.Application.Records;

public sealed record PartInput(
    string Name,
    string? Brand,
    string? PartNumber,
    decimal Quantity,
    PartUnit Unit,
    decimal? UnitPrice);

public sealed record RecordInput(
    RecordType Type,
    DateOnly Date,
    int? Odometer,
    string Title,
    string? Description,
    Category Category,
    string? PerformedBy,
    decimal? LaborCost,
    string? ReplacedOriginalPart,
    IReadOnlyList<PartInput> Parts);

public sealed record RecordListItemDto(
    Guid Id,
    RecordType Type,
    DateOnly Date,
    int? Odometer,
    string Title,
    Category Category,
    decimal TotalCost,
    int AttachmentCount);

public sealed record PartDto(
    string Name,
    string? Brand,
    string? PartNumber,
    decimal Quantity,
    PartUnit Unit,
    decimal? UnitPrice,
    decimal LineTotal);

public sealed record RecordDto(
    Guid Id,
    RecordType Type,
    DateOnly Date,
    int? Odometer,
    string Title,
    string? Description,
    Category Category,
    string? PerformedBy,
    decimal? LaborCost,
    string? ReplacedOriginalPart,
    IReadOnlyList<PartDto> Parts,
    decimal TotalCost,
    IReadOnlyList<AttachmentDto> Attachments,
    uint RowVersion);
