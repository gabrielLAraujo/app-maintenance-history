using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Catalog;
using Garage.Application.Catalog.GetCatalogDetails;

namespace Garage.Api.Catalog;

internal sealed class CatalogDetailsEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/catalog/{type}/makes/{makeId}/models/{modelId}/years/{yearId}", HandleAsync)
            .RequireRateLimiting(RateLimitPolicies.Catalog)
            .WithName("GetCatalogDetails")
            .WithTags("Catalog")
            .Produces<CatalogDetails>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        string type, string makeId, string modelId, string yearId, ISender sender, CancellationToken ct)
    {
        if (!CatalogTypeRoute.TryParse(type, out var vehicleType))
            return CatalogTypeRoute.Invalid();

        var result = await sender.Send(new GetCatalogDetailsQuery(vehicleType, makeId, modelId, yearId), ct);

        return result.ToHttpResult(details => TypedResults.Ok(details));
    }
}
