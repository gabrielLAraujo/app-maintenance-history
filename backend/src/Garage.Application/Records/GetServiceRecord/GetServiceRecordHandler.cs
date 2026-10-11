using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Attachments;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Records.GetServiceRecord;

internal sealed class GetServiceRecordHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db) : IQueryHandler<GetServiceRecordQuery, RecordDto>
{
    public async Task<Result<RecordDto>> Handle(GetServiceRecordQuery query, CancellationToken ct)
    {
        if (!await access.CanReadAsync(query.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var attachments = db.Query<Attachment>();
        var row = await db.Query<ServiceRecord>()
            .Where(r => r.VehicleId == query.VehicleId && r.Id == query.RecordId)
            .Select(r => new
            {
                r.Id,
                r.Type,
                r.Date,
                r.Odometer,
                r.Title,
                r.Description,
                r.Category,
                r.PerformedBy,
                r.LaborCost,
                r.ReplacedOriginalPart,
                // Na ordem em que o usuário informou as peças (Part.Position, interna ao domínio).
                Parts = r.Parts
                    .OrderBy(p => EF.Property<int>(p, "Position"))
                    .Select(p => new { p.Name, p.Brand, p.PartNumber, p.Quantity, p.Unit, p.UnitPrice })
                    .ToList(),
                // Todos os anexos (o dono vê também documentos e fotos privadas), na ordem de envio.
                Attachments = attachments
                    .Where(a => a.RecordId == r.Id)
                    .OrderBy(a => a.UploadedAt)
                    .ThenBy(a => a.Id)
                    .Select(a => new AttachmentDto(a.Id, a.Kind, a.IsPublic, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAt))
                    .ToList(),
                RowVersion = EF.Property<uint>(r, "RowVersion")
            })
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return ServiceRecordErrors.NotFound;

        // Totais calculados em memória, pelas mesmas regras do domínio (Part.LineTotal e ServiceRecord.TotalCost).
        var parts = row.Parts
            .Select(p => new { Part = p, LineTotal = RecordMapping.LineTotal(p.Quantity, p.UnitPrice) })
            .ToList();

        return new RecordDto(
            row.Id,
            row.Type,
            row.Date,
            row.Odometer?.Km,
            row.Title,
            row.Description,
            row.Category,
            row.PerformedBy,
            row.LaborCost?.Amount,
            row.ReplacedOriginalPart,
            parts
                .Select(p => new PartDto(
                    p.Part.Name,
                    p.Part.Brand,
                    p.Part.PartNumber,
                    p.Part.Quantity,
                    p.Part.Unit,
                    p.Part.UnitPrice?.Amount,
                    p.LineTotal.Amount))
                .ToList(),
            RecordMapping.TotalCost(row.LaborCost, parts.Select(p => p.LineTotal)).Amount,
            row.Attachments,
            row.RowVersion);
    }
}
