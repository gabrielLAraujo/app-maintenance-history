using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Attachments.DeleteAttachment;

namespace Garage.Api.Attachments;

internal sealed class DeleteAttachmentEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapDelete("/vehicles/{vehicleId}/attachments/{attachmentId}", HandleAsync)
            .WithName("DeleteAttachment")
            .WithTags("Attachments")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            // Exclusão concorrente do mesmo anexo: o DELETE do segundo não afeta linha nenhuma (concurrency.conflict).
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> HandleAsync(Guid vehicleId, Guid attachmentId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteAttachmentCommand(vehicleId, attachmentId), ct);

        return result.ToNoContent();
    }
}
