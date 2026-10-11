using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Records;
using Garage.Application.Records.GetServiceRecord;

namespace Garage.Api.Records;

internal sealed class GetRecordEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles/{vehicleId}/records/{recordId}", HandleAsync)
            .WithName("GetRecord")
            .WithTags("Records")
            .Produces<RecordDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(Guid vehicleId, Guid recordId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetServiceRecordQuery(vehicleId, recordId), ct);

        return result.ToHttpResult(record => TypedResults.Ok(record));
    }
}
