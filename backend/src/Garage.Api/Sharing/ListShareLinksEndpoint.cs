using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Sharing;
using Garage.Application.Sharing.GetShareLinks;

namespace Garage.Api.Sharing;

internal sealed class ListShareLinksEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles/{vehicleId}/share-links", HandleAsync)
            .WithName("ListShareLinks")
            .WithTags("ShareLinks")
            .Produces<IReadOnlyList<ShareLinkDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(Guid vehicleId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetShareLinksQuery(vehicleId), ct);

        return result.ToHttpResult(links => TypedResults.Ok(links));
    }
}
