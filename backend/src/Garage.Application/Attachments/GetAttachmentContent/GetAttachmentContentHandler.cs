using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Garage.Application.Attachments.GetAttachmentContent;

internal sealed partial class GetAttachmentContentHandler(
    ICurrentUser currentUser,
    IVehicleAccess access,
    IReadDbContext db,
    IFileStorage storage,
    ILogger<GetAttachmentContentHandler> logger) : IQueryHandler<GetAttachmentContentQuery, FileContent>
{
    public async Task<Result<FileContent>> Handle(GetAttachmentContentQuery query, CancellationToken ct)
    {
        if (!await access.CanReadAsync(query.VehicleId, currentUser.UserId, ct))
            return VehicleErrors.NotFound;

        var attachment = await db.Query<Attachment>()
            .Where(a => a.VehicleId == query.VehicleId && a.Id == query.AttachmentId)
            .Select(a => new { a.StorageKey, a.ContentType, a.FileName })
            .FirstOrDefaultAsync(ct);
        if (attachment is null)
            return AttachmentErrors.NotFound;

        var content = await storage.OpenReadAsync(attachment.StorageKey, ct);
        if (content is null)
        {
            // Linha sem arquivo: perda de dados no armazenamento (ex.: pasta fora de um volume persistente).
            LogFileMissing(logger, query.AttachmentId);
            return AttachmentErrors.NotFound;
        }

        return new FileContent(content, attachment.ContentType, attachment.FileName);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Attachment {AttachmentId} has no file in storage")]
    private static partial void LogFileMissing(ILogger logger, Guid attachmentId);
}
