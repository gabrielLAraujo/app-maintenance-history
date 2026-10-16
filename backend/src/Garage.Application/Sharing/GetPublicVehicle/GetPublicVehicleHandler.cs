using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Application.Records;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Sharing;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Garage.Application.Sharing.GetPublicVehicle;

/// <summary>
/// Anônimo: o token é a única credencial. Monta o read model público (spec 4.8) campo a campo; tudo o que não está na
/// allowlist fica fora já na consulta (chassi, observações, lembretes, documentos, fotos privadas, ids internos).
/// </summary>
internal sealed class GetPublicVehicleHandler(IReadDbContext db, IAppClock clock)
    : IQueryHandler<GetPublicVehicleQuery, PublicVehicleDto>
{
    public async Task<Result<PublicVehicleDto>> Handle(GetPublicVehicleQuery query, CancellationToken ct)
    {
        // S1: malformado, inexistente, revogado e expirado são a mesma resposta. O malformado nem chega ao banco.
        if (!ShareTokens.LooksValid(query.Token))
            return ShareLinkErrors.NotFound;

        var link = await db.Query<ShareLink>().FirstOrDefaultAsync(l => l.Token == query.Token, ct);
        if (link is null || !link.IsValidAt(clock.Now))
            return ShareLinkErrors.NotFound;

        var vehicle = await db.Query<Vehicle>()
            .Where(v => v.Id == link.VehicleId)
            .Select(v => new
            {
                v.Make,
                v.Model,
                v.Version,
                v.Generation,
                v.Year.Manufacture,
                ModelYear = v.Year.Model,
                v.Fuel,
                v.Nickname,
                Plate = v.Plate == null ? null : v.Plate.Value,
                CurrentOdometer = v.CurrentOdometer.Km,
                // A capa só aparece se a foto for pública (spec 4.3); a capa do dono não muda.
                CoverPhotoId = db.Query<Attachment>()
                    .Where(a => a.Id == v.CoverPhotoId && a.VehicleId == v.Id && a.Kind == AttachmentKind.Photo && a.IsPublic)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
        if (vehicle is null)
            return ShareLinkErrors.NotFound;

        var (records, nextCursor) = await GetRecordPageAsync(link, query.Cursor, ct);

        return new PublicVehicleDto(
            vehicle.Make,
            vehicle.Model,
            vehicle.Version,
            vehicle.Generation,
            vehicle.Manufacture,
            vehicle.ModelYear,
            vehicle.Fuel,
            vehicle.Nickname,
            link.IncludePlate ? vehicle.Plate : null,
            vehicle.CurrentOdometer,
            link.IncludeCosts,
            vehicle.CoverPhotoId is { } coverId
                ? new PublicPhotoDto(coverId, ShareLinkMapping.PhotoPath(link.Token, coverId))
                : null,
            records,
            nextCursor);
    }

    /// <summary>Uma página do histórico na mesma ordem e com o mesmo cursor da listagem do dono.</summary>
    private async Task<(IReadOnlyList<PublicRecordDto> Records, string? NextCursor)> GetRecordPageAsync(
        ShareLink link, string? cursor, CancellationToken ct)
    {
        const int pageSize = GetPublicVehicleQuery.PageSize;

        var records = db.Query<ServiceRecord>().Where(r => r.VehicleId == link.VehicleId);
        // O validator já recusou cursores inválidos: aqui só falta o ausente.
        if (RecordCursor.TryDecode(cursor, out var after))
        {
            var (date, createdAt, id) = after;
            records = records.Where(r =>
                r.Date < date
                || (r.Date == date && (r.CreatedAt < createdAt || (r.CreatedAt == createdAt && r.Id.CompareTo(id) < 0))));
        }

        // Um item além da página indica que há próxima. Id e CreatedAt só servem para o cursor; não vão para o DTO.
        var attachments = db.Query<Attachment>();
        var rows = await records
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(pageSize + 1)
            .Select(r => new
            {
                r.Id,
                r.CreatedAt,
                r.Type,
                r.Date,
                r.Odometer,
                r.Category,
                r.Title,
                r.Description,
                r.PerformedBy,
                r.ReplacedOriginalPart,
                r.LaborCost,
                Parts = r.Parts
                    .OrderBy(p => EF.Property<int>(p, "Position"))
                    .Select(p => new { p.Name, p.Brand, p.PartNumber, p.Quantity, p.Unit, p.UnitPrice })
                    .ToList(),
                // Só fotos marcadas como públicas; documentos nunca (A4).
                PhotoIds = attachments
                    .Where(a => a.RecordId == r.Id && a.Kind == AttachmentKind.Photo && a.IsPublic)
                    .OrderBy(a => a.UploadedAt)
                    .ThenBy(a => a.Id)
                    .Select(a => a.Id)
                    .ToList()
            })
            .ToListAsync(ct);

        var page = rows.Take(pageSize).ToList();
        var last = page.LastOrDefault();
        var nextCursor = rows.Count > pageSize && last is not null
            ? RecordCursor.Encode(last.Date, last.CreatedAt, last.Id)
            : null;

        // Preço das peças, mão de obra e total só com IncludeCosts; o total segue a regra do domínio (ServiceRecord.TotalCost).
        decimal? Cost(Money? money) => link.IncludeCosts ? money?.Amount : null;

        IReadOnlyList<PublicRecordDto> items = page
            .Select(row => new PublicRecordDto(
                row.Type,
                row.Date,
                row.Odometer?.Km,
                row.Category,
                row.Title,
                row.Description,
                row.PerformedBy,
                row.ReplacedOriginalPart,
                row.Parts
                    .Select(part => new PublicPartDto(
                        part.Name, part.Brand, part.PartNumber, part.Quantity, part.Unit, Cost(part.UnitPrice)))
                    .ToList(),
                Cost(row.LaborCost),
                Cost(RecordMapping.TotalCost(
                    row.LaborCost,
                    row.Parts.Select(part => RecordMapping.LineTotal(part.Quantity, part.UnitPrice)))),
                row.PhotoIds
                    .Select(photoId => new PublicPhotoDto(photoId, ShareLinkMapping.PhotoPath(link.Token, photoId)))
                    .ToList()))
            .ToList();
        return (items, nextCursor);
    }
}
