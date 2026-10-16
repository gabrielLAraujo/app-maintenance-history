using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;

namespace Garage.Api.Vehicles;

internal sealed class CreateVehicleEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/vehicles", HandleAsync)
            .WithName("CreateVehicle")
            .WithTags("Vehicles")
            .Produces<CreatedIdResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> HandleAsync(CreateVehicleRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(), ct);

        return result.ToHttpResult(id => TypedResults.Created($"/api/vehicles/{id}", new CreatedIdResponse(id)));
    }
}
