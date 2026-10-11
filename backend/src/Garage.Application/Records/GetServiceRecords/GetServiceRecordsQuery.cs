using Garage.Application.Abstractions;
using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Records;

namespace Garage.Application.Records.GetServiceRecords;

public sealed record GetServiceRecordsQuery(
    Guid VehicleId,
    RecordType? Type,
    Category? Category,
    string? Cursor,
    int PageSize = GetServiceRecordsQuery.DefaultPageSize) : IQuery<PagedResult<RecordListItemDto>>
{
    public const int DefaultPageSize = 30;

    public const int MaxPageSize = 100;
}
