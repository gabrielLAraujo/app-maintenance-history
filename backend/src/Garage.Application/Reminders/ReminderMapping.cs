using Garage.Domain.Common;
using Garage.Domain.Reminders;

namespace Garage.Application.Reminders;

internal static class ReminderMapping
{
    /// <summary>O título, que o validator já aprovou, sem os espaços das pontas.</summary>
    public static string CleanTitle(this ReminderInput input) => input.Title.Trim();

    public static ReminderInterval ToInterval(this ReminderInput input) => new(input.IntervalKm, input.IntervalMonths);

    /// <summary>A km da última vez, se informada; o validator já garantiu que está no intervalo do <see cref="Odometer"/>.</summary>
    public static Odometer? ToBaseOdometer(this ReminderInput input) =>
        input.LastDoneOdometer is { } km ? new Odometer(km) : null;

    /// <summary>
    /// Junta o status calculado à data da km do veículo, para a UI avisar quando a leitura estiver velha.
    /// </summary>
    public static ReminderStatusDto ToDto(this ReminderStatus status, DateTimeOffset odometerUpdatedAt) =>
        new(status.State, status.KmRemaining, status.DaysRemaining, odometerUpdatedAt);
}
