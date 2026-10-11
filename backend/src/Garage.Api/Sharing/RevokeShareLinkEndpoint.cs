using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Sharing.RevokeShareLink;

namespace Garage.Api.Sharing;

internal sealed class RevokeShareLinkEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapDelete("/vehicles/{vehicleId}/share-links/{linkId}", HandleAsync)
            .WithName("RevokeShareLink")
            .WithTags("ShareLinks")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            // Link (ou veículo) excluído entre a leitura e a gravação: o UPDATE não afeta linha nenhuma (concurrency.conflict).
            .ProducesProblem(StatusCodes.Status409Conflict);

    // Revoga (S2): o link continua na lista, inativo. Revogar de novo responde 204 e mantém o primeiro instante.
    private static async Task<IResult> HandleAsync(Guid vehicleId, Guid linkId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new RevokeShareLinkCommand(vehicleId, linkId), ct);

        return result.ToNoContent();
    }
}
