using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Reminders;
using Garage.Application.Reminders.GetVehicleReminders;

namespace Garage.Api.Reminders;

internal sealed class ListRemindersEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/vehicles/{vehicleId}/reminders", HandleAsync)
            .WithName("ListReminders")
            .WithTags("Reminders")
            .Produces<IReadOnlyList<ReminderDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(Guid vehicleId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetVehicleRemindersQuery(vehicleId), ct);

        return result.ToHttpResult(reminders => TypedResults.Ok(reminders));
    }
}
