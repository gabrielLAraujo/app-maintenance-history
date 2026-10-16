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
    // campo kind. Acima do teto o corpo não é lido; entre 10 MB e o teto, quem recusa é o caso de uso. Ambos: too_large.
    private const long MaxRequestBytes = Attachment.MaxSizeBytes + 1024 * 1024;

    private const string FileField = "file";
    private const string KindField = "kind";

    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/vehicles/{vehicleId}/records/{recordId}/attachments", HandleAsync)
            .WithName("UploadAttachment")
            .WithTags("Attachments")
            // O AntiforgeryValidationMiddleware já exige o X-XSRF-TOKEN em todo POST; a validação nativa dos formulários
            // rodaria antes da autenticação e recusaria o token, que é vinculado ao usuário.
            .DisableAntiforgery()
            .RequireRateLimiting(RateLimitPolicies.Uploads)
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .Accepts<UploadAttachmentForm>("multipart/form-data")
            .Produces<AttachmentDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    // O formulário é lido aqui, não pelo bind do framework: com o corpo acima do teto, o bind falharia antes do handler e o
    // framework responderia 413 sem corpo — justamente no caso mais comum de arquivo grande demais (fotos de 12–25 MB, PDFs
    // escaneados), que ficaria sem attachment.too_large.
    private static async Task<IResult> HandleAsync(
        Guid vehicleId, Guid recordId, HttpRequest request, ISender sender, CancellationToken ct)
    {
        var (form, readError) = await ReadFormAsync(request, ct);
        if (readError is not null)
            return readError.ToProblem();

        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);
        var file = form.Files.GetFile(FileField);
        if (file is null)
            errors[FileField] = ["Selecione um arquivo."];

        // kind é obrigatório e não tem padrão: assumir foto tornaria pública, por omissão, uma foto de nota fiscal. É
        // convertido pelo nome, sem diferenciar maiúsculas; número, nome desconhecido ou repetido → validation.failed em kind.
        var kind = form[KindField].ToString();
        var attachmentKind = ParseKind(kind);
        if (attachmentKind is null)
            errors[KindField] = [kind.Length == 0 ? "Informe o tipo do anexo." : "Tipo de anexo inválido."];

        if (errors.Count > 0)
            return Error.Validation(errors).ToProblem();

        await using var content = file!.OpenReadStream();
        var command = new UploadAttachmentCommand(vehicleId, recordId, content, file.Length, file.FileName, attachmentKind!.Value);
        var result = await sender.Send(command, ct);

        return result.ToHttpResult(attachment =>
            TypedResults.Created($"/api/vehicles/{vehicleId}/attachments/{attachment.Id}/content", attachment));
    }

    private static async Task<(IFormCollection Form, Error? Error)> ReadFormAsync(HttpRequest request, CancellationToken ct)
    {
        // Content-Length acima do teto: recusado sem ler o corpo.
        if (request.ContentLength > MaxRequestBytes)
            return (FormCollection.Empty, AttachmentErrors.TooLarge);

        // Sem multipart não há arquivo nem tipo: a validação aponta os dois campos.
        if (!request.HasFormContentType)
            return (FormCollection.Empty, null);

        try
        {
            return (await request.ReadFormAsync(ct), null);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // Corpo em partes (sem Content-Length): o servidor só percebe o teto no meio da leitura.
            return (FormCollection.Empty, AttachmentErrors.TooLarge);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            // Multipart malformado ou corpo interrompido (BadHttpRequestException também é IOException).
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [FileField] = ["Não foi possível ler o arquivo enviado. Tente de novo."]
            };
            return (FormCollection.Empty, Error.Validation(errors));
        }
    }

    private static AttachmentKind? ParseKind(string value)
    {
        var name = Enum.GetNames<AttachmentKind>().FirstOrDefault(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
        return name is null ? null : Enum.Parse<AttachmentKind>(name);
    }
}
