using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Attachments;
using Garage.Application.Sharing.GetPublicAttachment;
using Microsoft.Net.Http.Headers;

namespace Garage.Api.Public;

internal sealed class GetPublicAttachmentEndpoint : IEndpoint
{
    // Quem já viu a foto pode mantê-la no cache do navegador por até 1 h; revogar o link (ou ocultar a foto) continua
    // barrando as cargas novas, que sempre passam pela checagem do token.
    private const string PhotoCache = "private, max-age=3600";

    // Os erros (404) não ficam em cache: a foto pode voltar a ser pública.
    private const string ErrorCache = "no-store";

    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/public/{token}/attachments/{attachmentId}", HandleAsync)
            .AllowAnonymous()
            // Cota própria, separada da visão JSON: uma página pública carrega dezenas de fotos de uma vez.
            .RequireRateLimiting(RateLimitPolicies.PublicPhotos)
            .AddEndpointFilter<PublicHeadersFilter>()
            .WithName("GetPublicAttachment")
            .WithTags("Public")
            .Produces(StatusCodes.Status200OK, typeof(Stream), "image/jpeg", "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        string token, Guid attachmentId, ISender sender, HttpResponse response, CancellationToken ct)
    {
        var result = await sender.Send(new GetPublicAttachmentQuery(token, attachmentId), ct);

        response.Headers.CacheControl = ErrorCache;
        return result.ToHttpResult(file => ToInlineImage(file, response));
    }

    // Só fotos chegam aqui: sempre inline (spec 10.4). O X-Content-Type-Options: nosniff vem do SecurityHeadersMiddleware.
    private static IResult ToInlineImage(FileContent file, HttpResponse response)
    {
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(file.FileName);
        response.Headers.ContentDisposition = disposition.ToString();
        response.Headers.CacheControl = PhotoCache;
        return TypedResults.Stream(file.Content, file.ContentType);
    }
}
