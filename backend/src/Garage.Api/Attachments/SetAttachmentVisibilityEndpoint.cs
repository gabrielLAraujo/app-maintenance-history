using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Attachments.SetAttachmentVisibility;
using Garage.Domain.Common;

namespace Garage.Api.Attachments;

internal sealed class SetAttachmentVisibilityEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPatch("/vehicles/{vehicleId}/attachments/{attachmentId}", HandleAsync)
            .WithName("SetAttachmentVisibility")
            .WithTags("Attachments")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            // Anexo excluído entre a leitura e a gravação: o UPDATE não afeta linha nenhuma (concurrency.conflict).
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, Guid attachmentId, SetAttachmentVisibilityRequest request, ISender sender, CancellationToken ct)
    {
        if (request.IsPublic is not { } isPublic)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["isPublic"] = ["Informe se o anexo aparece no link público."]
            };
            return Error.Validation(errors).ToProblem();
        }

        var result = await sender.Send(new SetAttachmentVisibilityCommand(vehicleId, attachmentId, isPublic), ct);

        return result.ToNoContent();
    }
}
