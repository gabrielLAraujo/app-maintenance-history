using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;

namespace Garage.Api.Records;

internal sealed class UpdateRecordEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPut("/vehicles/{vehicleId}/records/{recordId}", HandleAsync)
            .WithName("UpdateRecord")
            .WithTags("Records")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, Guid recordId, UpdateRecordRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(vehicleId, recordId), ct);

        return result.ToNoContent();
    }
}
