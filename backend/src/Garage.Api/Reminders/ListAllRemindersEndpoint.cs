using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Reminders;
using Garage.Application.Reminders.GetAllReminders;

namespace Garage.Api.Reminders;

internal sealed class ListAllRemindersEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/reminders", HandleAsync)
            .WithName("ListAllReminders")
            .WithTags("Reminders")
            .Produces<IReadOnlyList<DueReminderDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetAllRemindersQuery(), ct);

        return result.ToHttpResult(reminders => TypedResults.Ok(reminders));
    }
}
