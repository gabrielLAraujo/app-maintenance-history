using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Catalog;
using Garage.Application.Catalog.GetCatalogMakes;

namespace Garage.Api.Catalog;

internal sealed class CatalogMakesEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/catalog/{type}/makes", HandleAsync)
            .RequireRateLimiting(RateLimitPolicies.Catalog)
            .WithName("GetCatalogMakes")
            .WithTags("Catalog")
            .Produces<IReadOnlyList<CatalogMake>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(string type, ISender sender, CancellationToken ct)
    {
        if (!CatalogTypeRoute.TryParse(type, out var vehicleType))
            return CatalogTypeRoute.Invalid();

        var result = await sender.Send(new GetCatalogMakesQuery(vehicleType), ct);

        return result.ToHttpResult(makes => TypedResults.Ok(makes));
    }
}
