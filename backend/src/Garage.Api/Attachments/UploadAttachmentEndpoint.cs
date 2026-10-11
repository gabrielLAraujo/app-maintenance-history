using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Attachments;
using Garage.Application.Attachments.UploadAttachment;
using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Api.Attachments;

internal sealed class UploadAttachmentEndpoint : IEndpoint
{
    // Teto da requisição no servidor (spec 10.4): o arquivo pode ter até 10 MB (A1) e a folga cobre o envelope multipart e o
    // campo kind. Entre 10 MB e o teto, o caso de uso responde attachment.too_large; acima, o Kestrel corta a leitura (413).
    private const long MaxRequestBytes = Attachment.MaxSizeBytes + 1024 * 1024;

    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/vehicles/{vehicleId}/records/{recordId}/attachments", HandleAsync)
            .WithName("UploadAttachment")
            .WithTags("Attachments")
            // O AntiforgeryValidationMiddleware já exige o X-XSRF-TOKEN em todo POST; a validação nativa dos formulários
            // rodaria antes da autenticação e recusaria o token, que é vinculado ao usuário.
            .DisableAntiforgery()
            .RequireRateLimiting(RateLimitPolicies.Uploads)
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .Produces<AttachmentDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    // kind é obrigatório e não tem padrão: assumir foto tornaria pública, por omissão, uma foto de nota fiscal. Chega como
    // texto e é convertido pelo nome, sem diferenciar maiúsculas (o bind de enum diferencia e recusaria "photo" sem
    // dizer o campo); número ou nome desconhecido → validation.failed em "kind".
    private static async Task<IResult> HandleAsync(
        Guid vehicleId, Guid recordId, IFormFile? file, [FromForm] string? kind, ISender sender, CancellationToken ct)
    {
        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);
        if (file is null)
            errors["file"] = ["Selecione um arquivo."];

        var attachmentKind = ParseKind(kind);
        if (attachmentKind is null)
            errors["kind"] = [string.IsNullOrEmpty(kind) ? "Informe o tipo do anexo." : "Tipo de anexo inválido."];

        if (errors.Count > 0)
            return Error.Validation(errors).ToProblem();

        await using var content = file!.OpenReadStream();
        var command = new UploadAttachmentCommand(vehicleId, recordId, content, file.Length, file.FileName, attachmentKind!.Value);
        var result = await sender.Send(command, ct);

        return result.ToHttpResult(attachment =>
            TypedResults.Created($"/api/vehicles/{vehicleId}/attachments/{attachment.Id}/content", attachment));
    }

    private static AttachmentKind? ParseKind(string? value)
    {
        var name = Enum.GetNames<AttachmentKind>().FirstOrDefault(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
        return name is null ? null : Enum.Parse<AttachmentKind>(name);
    }
}
