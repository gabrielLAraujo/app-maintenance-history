using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Vehicles;
using Garage.Application.Vehicles.GetVehicles;

namespace Garage.Api.Vehicles;

internal sealed class ListVehiclesEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles", HandleAsync)
            .WithName("ListVehicles")
            .WithTags("Vehicles")
            .Produces<IReadOnlyList<VehicleSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetVehiclesQuery(), ct);

        return result.ToHttpResult(vehicles => TypedResults.Ok(vehicles));
    }
}
