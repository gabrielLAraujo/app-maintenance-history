using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Sharing;

namespace Garage.Api.Sharing;

internal sealed class CreateShareLinkEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/vehicles/{vehicleId}/share-links", HandleAsync)
            .WithName("CreateShareLink")
            .WithTags("ShareLinks")
            .Produces<ShareLinkDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, CreateShareLinkRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(vehicleId), ct);

        return result.ToHttpResult(link =>
            TypedResults.Created($"/api/vehicles/{vehicleId}/share-links/{link.Id}", link));
    }
}
