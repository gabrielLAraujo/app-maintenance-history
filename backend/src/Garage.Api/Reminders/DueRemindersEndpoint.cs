using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Reminders;
using Garage.Application.Reminders.GetDueReminders;

namespace Garage.Api.Reminders;

internal sealed class DueRemindersEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api) =>
        api.MapGet("/reminders/due", HandleAsync)
            .WithName("ListDueReminders")
            .WithTags("Reminders")
            .Produces<IReadOnlyList<DueReminderDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetDueRemindersQuery(), ct);

        return result.ToHttpResult(due => TypedResults.Ok(due));
    }
}
