using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;

namespace Garage.Api.Vehicles;

internal sealed class CorrectOdometerEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/vehicles/{vehicleId}/odometer/correction", HandleAsync)
            .WithName("CorrectOdometer")
            .WithTags("Vehicles")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, CorrectOdometerRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(vehicleId), ct);

        return result.ToNoContent();
    }
}
