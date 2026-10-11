using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Reminders.DeleteReminder;

namespace Garage.Api.Reminders;

internal sealed class DeleteReminderEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapDelete("/vehicles/{vehicleId}/reminders/{reminderId}", HandleAsync)
            .WithName("DeleteReminder")
            .WithTags("Reminders")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> HandleAsync(Guid vehicleId, Guid reminderId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteReminderCommand(vehicleId, reminderId), ct);

        return result.ToNoContent();
    }
}
