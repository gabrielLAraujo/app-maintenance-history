using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Garage;
using Garage.Application.Garage.GetGarageSummary;

namespace Garage.Api.Garage;

internal sealed class GetGarageSummaryEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/garage/summary", HandleAsync)
            .WithName("GetGarageSummary")
            .WithTags("Garage")
            .Produces<GarageSummaryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetGarageSummaryQuery(), ct);

        return result.ToHttpResult(summary => TypedResults.Ok(summary));
    }
}
