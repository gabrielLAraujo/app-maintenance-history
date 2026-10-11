using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;

namespace Garage.Api.Records;

internal sealed class AddRecordEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/vehicles/{vehicleId}/records", HandleAsync)
            .WithName("AddRecord")
            .WithTags("Records")
            .Produces<CreatedIdResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, AddRecordRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(vehicleId), ct);

        return result.ToHttpResult(id =>
            TypedResults.Created($"/api/vehicles/{vehicleId}/records/{id}", new CreatedIdResponse(id)));
    }
}
