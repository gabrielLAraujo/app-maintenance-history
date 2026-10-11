using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;

namespace Garage.Api.Reminders;

internal sealed class CreateReminderEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPost("/vehicles/{vehicleId}/reminders", HandleAsync)
            .WithName("CreateReminder")
            .WithTags("Reminders")
            .Produces<CreatedIdResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, CreateReminderRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(vehicleId), ct);

        return result.ToHttpResult(id =>
            TypedResults.Created($"/api/vehicles/{vehicleId}/reminders/{id}", new CreatedIdResponse(id)));
    }
}
