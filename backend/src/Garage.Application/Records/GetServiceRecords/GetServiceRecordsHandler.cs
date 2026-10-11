using Garage.Application.Abstractions;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Records.GetServiceRecords;

internal sealed class GetServiceRecordsHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db) : IQueryHandler<GetServiceRecordsQuery, PagedResult<RecordListItemDto>>
{
    public async Task<Result<PagedResult<RecordListItemDto>>> Handle(GetServiceRecordsQuery query, CancellationToken ct)
    {
        if (!await access.CanReadAsync(query.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var records = db.Query<ServiceRecord>().Where(r => r.VehicleId == query.VehicleId);
        if (query.Type is { } type)
            records = records.Where(r => r.Type == type);
        if (query.Category is { } category)
            records = records.Where(r => r.Category == category);

        // O validator já recusou cursores inválidos: aqui só falta o ausente.
        if (RecordCursor.TryDecode(query.Cursor, out var after))
        {
            var (date, createdAt, id) = after;
            records = records.Where(r =>
                r.Date < date
                || (r.Date == date && (r.CreatedAt < createdAt || (r.CreatedAt == createdAt && r.Id.CompareTo(id) < 0))));
        }

        // Um item além da página indica que há próxima. O total não é coluna: é calculado em memória a partir das peças.
        var rows = await records
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(query.PageSize + 1)
            .Select(r => new
            {
                r.Id,
                r.Type,
                r.Date,
                r.Odometer,
                r.Title,
                r.Category,
                r.CreatedAt,
                r.LaborCost,
                Parts = r.Parts
                    .OrderBy(p => EF.Property<int>(p, "Position"))
                    .Select(p => new { p.Quantity, p.UnitPrice })
                    .ToList()
            })
            .ToListAsync(ct);

        var page = rows.Take(query.PageSize).ToList();
        var last = page.LastOrDefault();
        var nextCursor = rows.Count > query.PageSize && last is not null
            ? RecordCursor.Encode(last.Date, last.CreatedAt, last.Id)
            : null;

        IReadOnlyList<RecordListItemDto> items = page
            .Select(row => new RecordListItemDto(
                row.Id,
                row.Type,
                row.Date,
                row.Odometer?.Km,
                row.Title,
                row.Category,
                RecordMapping.TotalCost(
                        row.LaborCost,
                        row.Parts.Select(part => RecordMapping.LineTotal(part.Quantity, part.UnitPrice)))
                    .Amount))
            .ToList();
        return new PagedResult<RecordListItemDto>(items, nextCursor);
    }
}
