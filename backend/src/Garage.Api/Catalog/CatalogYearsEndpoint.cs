using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Catalog;
using Garage.Application.Catalog.GetCatalogYears;

namespace Garage.Api.Catalog;

internal sealed class CatalogYearsEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/catalog/{type}/makes/{makeId}/models/{modelId}/years", HandleAsync)
            .RequireRateLimiting(RateLimitPolicies.Catalog)
            .WithName("GetCatalogYears")
            .WithTags("Catalog")
            .Produces<IReadOnlyList<CatalogYear>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        string type, string makeId, string modelId, ISender sender, CancellationToken ct)
    {
        if (!CatalogTypeRoute.TryParse(type, out var vehicleType))
            return CatalogTypeRoute.Invalid();

        var result = await sender.Send(new GetCatalogYearsQuery(vehicleType, makeId, modelId), ct);

        return result.ToHttpResult(years => TypedResults.Ok(years));
    }
}
