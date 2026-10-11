using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Attachments;
using Garage.Application.Attachments.GetAttachmentContent;
using Microsoft.Net.Http.Headers;

namespace Garage.Api.Attachments;

internal sealed class GetAttachmentContentEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles/{vehicleId}/attachments/{attachmentId}/content", HandleAsync)
            .WithName("GetAttachmentContent")
            .WithTags("Attachments")
            .Produces(StatusCodes.Status200OK, typeof(Stream), "image/jpeg", "image/png", "application/pdf")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, Guid attachmentId, ISender sender, HttpResponse response, CancellationToken ct)
    {
        var result = await sender.Send(new GetAttachmentContentQuery(vehicleId, attachmentId), ct);

        return result.ToHttpResult(file => ToFileResult(file, response));
    }

    // Spec 10.4: imagens abrem no navegador (inline); o resto — os PDFs — sempre baixa como arquivo, com o nome original.
    // O X-Content-Type-Options: nosniff vem do SecurityHeadersMiddleware.
    private static IResult ToFileResult(FileContent file, HttpResponse response)
    {
        if (!file.ContentType.StartsWith("image/", StringComparison.Ordinal))
            return TypedResults.File(file.Content, file.ContentType, file.FileName);

        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(file.FileName);
        response.Headers.ContentDisposition = disposition.ToString();
        return TypedResults.Stream(file.Content, file.ContentType);
    }
}
