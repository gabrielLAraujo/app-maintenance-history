using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Records;
using Garage.Application.Records.GetServiceRecords;
using Garage.Domain.Common;
using Garage.Domain.Records;

namespace Garage.Api.Records;

internal sealed class ListRecordsEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles/{vehicleId}/records", HandleAsync)
            .WithName("ListRecords")
            .WithTags("Records")
            .Produces<PagedResult<RecordListItemDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    // Os filtros chegam como no JSON (camelCase), mas o bind padrão de enum na query diferencia maiúsculas: por isso
    // são lidos como texto e convertidos aqui pelo nome, sem diferenciar maiúsculas. Nome desconhecido → validation.failed.
    private static async Task<IResult> HandleAsync(
        Guid vehicleId, string? type, string? category, string? cursor, int? pageSize, ISender sender, CancellationToken ct)
    {
        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);
        var recordType = ParseFilter<RecordType>(type, "type", "Tipo de registro inválido.", errors);
        var recordCategory = ParseFilter<Category>(category, "category", "Categoria inválida.", errors);
        if (errors.Count > 0)
            return Error.Validation(errors).ToProblem();

        var query = new GetServiceRecordsQuery(
            vehicleId, recordType, recordCategory, cursor, pageSize ?? GetServiceRecordsQuery.DefaultPageSize);
        var result = await sender.Send(query, ct);

        return result.ToHttpResult(page => TypedResults.Ok(page));
    }

    private static TEnum? ParseFilter<TEnum>(string? value, string field, string message, Dictionary<string, string[]> errors)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var name = Enum.GetNames<TEnum>().FirstOrDefault(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (name is not null)
            return Enum.Parse<TEnum>(name);

        errors[field] = [message];
        return null;
    }
}
