using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Sharing;
using Garage.Application.Sharing.GetPublicVehicle;

namespace Garage.Api.Public;

internal sealed class GetPublicVehicleEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/public/{token}", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Public)
            .AddEndpointFilter<PublicHeadersFilter>()
            .WithName("GetPublicVehicle")
            .WithTags("Public")
            .Produces<PublicVehicleDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        string token, string? cursor, ISender sender, HttpResponse response, CancellationToken ct)
    {
        var result = await sender.Send(new GetPublicVehicleQuery(token, cursor), ct);

        // A visão muda a cada edição do dono e deixa de existir quando o link é revogado ou expira: nunca vai para cache.
        response.Headers.CacheControl = "no-store";
        return result.ToHttpResult(view => TypedResults.Ok(view));
    }
}
