using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;

namespace Garage.Api.Reminders;

internal sealed class UpdateReminderEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapPut("/vehicles/{vehicleId}/reminders/{reminderId}", HandleAsync)
            .WithName("UpdateReminder")
            .WithTags("Reminders")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> HandleAsync(
        Guid vehicleId, Guid reminderId, UpdateReminderRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(vehicleId, reminderId), ct);

        return result.ToNoContent();
    }
}
