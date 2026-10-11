using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;

namespace Garage.Api.Vehicles;

internal sealed class UpdateVehicleEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPut("/vehicles/{vehicleId}", HandleAsync)
            .WithName("UpdateVehicle")
            .WithTags("Vehicles")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, UpdateVehicleRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(vehicleId), ct);

        return result.ToNoContent();
    }
}
