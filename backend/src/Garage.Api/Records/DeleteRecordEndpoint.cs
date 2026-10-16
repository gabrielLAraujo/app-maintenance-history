using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Records.DeleteServiceRecord;

namespace Garage.Api.Records;

internal sealed class DeleteRecordEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapDelete("/vehicles/{vehicleId}/records/{recordId}", HandleAsync)
            .WithName("DeleteRecord")
            .WithTags("Records")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> HandleAsync(Guid vehicleId, Guid recordId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteServiceRecordCommand(vehicleId, recordId), ct);

        return result.ToNoContent();
    }
}
