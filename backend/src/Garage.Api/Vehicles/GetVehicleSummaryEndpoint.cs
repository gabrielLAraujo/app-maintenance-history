using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Vehicles;
using Garage.Application.Vehicles.GetVehicleSummary;

namespace Garage.Api.Vehicles;

internal sealed class GetVehicleSummaryEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles/{vehicleId}/summary", HandleAsync)
            .WithName("GetVehicleSummary")
            .WithTags("Vehicles")
            .Produces<VehicleSpendingDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(Guid vehicleId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetVehicleSummaryQuery(vehicleId), ct);

        return result.ToHttpResult(summary => TypedResults.Ok(summary));
    }
}
