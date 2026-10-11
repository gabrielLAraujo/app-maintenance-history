using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Vehicles.DeleteVehicle;

namespace Garage.Api.Vehicles;

internal sealed class DeleteVehicleEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapDelete("/vehicles/{vehicleId}", HandleAsync)
            .WithName("DeleteVehicle")
            .WithTags("Vehicles")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> HandleAsync(Guid vehicleId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteVehicleCommand(vehicleId), ct);

        return result.ToNoContent();
    }
}
