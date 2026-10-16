using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;

namespace Garage.Api.Vehicles;

internal sealed class SetCoverPhotoEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPut("/vehicles/{vehicleId}/cover-photo", HandleAsync)
            .WithName("SetVehicleCoverPhoto")
            .WithTags("Vehicles")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, SetCoverPhotoRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(vehicleId), ct);

        return result.ToNoContent();
    }
}
