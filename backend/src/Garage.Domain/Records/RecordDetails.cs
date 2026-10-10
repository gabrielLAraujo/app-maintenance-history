using Garage.Domain.Common;

namespace Garage.Domain.Records;

public sealed record RecordDetails(
    RecordType Type,
    DateOnly Date,
    Odometer? Odometer,
    string Title,
    string? Description,
    Category Category,
    string? PerformedBy,
    Money? LaborCost,
    string? ReplacedOriginalPart,
    IReadOnlyList<PartData> Parts);
