using Garage.Domain.Common;

namespace Garage.Domain.Records;

public sealed record PartData(
    string Name,
    string? Brand,
    string? PartNumber,
    decimal Quantity,
    PartUnit Unit,
    Money? UnitPrice);
