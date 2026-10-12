using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Catalog;
using Garage.Application.Catalog.GetCatalogModels;

namespace Garage.Api.Catalog;

internal sealed class CatalogModelsEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/catalog/{type}/makes/{makeId}/models", HandleAsync)
            .RequireRateLimiting(RateLimitPolicies.Catalog)
            .WithName("GetCatalogModels")
            .WithTags("Catalog")
            .Produces<IReadOnlyList<CatalogModel>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(string type, string makeId, ISender sender, CancellationToken ct)
    {
        if (!CatalogTypeRoute.TryParse(type, out var vehicleType))
            return CatalogTypeRoute.Invalid();

        var result = await sender.Send(new GetCatalogModelsQuery(vehicleType, makeId), ct);

        return result.ToHttpResult(models => TypedResults.Ok(models));
    }
}
