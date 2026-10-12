using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Catalog;
using Garage.Application.Vehicles.GetVehicleFipePrice;

namespace Garage.Api.Vehicles;

internal sealed class GetVehicleFipePriceEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles/{vehicleId}/fipe-price", HandleAsync)
            .RequireRateLimiting(RateLimitPolicies.Catalog)
            .WithName("GetVehicleFipePrice")
            .WithTags("Vehicles")
            .Produces<CatalogPrice>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(Guid vehicleId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetVehicleFipePriceQuery(vehicleId), ct);

        return result.ToHttpResult(price => TypedResults.Ok(price));
    }
}
