using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Vehicles;
using Garage.Application.Vehicles.GetVehicle;

namespace Garage.Api.Vehicles;

internal sealed class GetVehicleEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles/{vehicleId}", HandleAsync)
            .WithName("GetVehicle")
            .WithTags("Vehicles")
            .Produces<VehicleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(Guid vehicleId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetVehicleQuery(vehicleId), ct);

        return result.ToHttpResult(vehicle => TypedResults.Ok(vehicle));
    }
}
