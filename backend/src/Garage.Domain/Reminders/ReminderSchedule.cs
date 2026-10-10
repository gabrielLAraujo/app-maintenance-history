namespace Garage.Domain.Reminders;

/// <summary>Cálculo puro do status de um lembrete (M2): o pior dos eixos presentes, "o que vier primeiro".</summary>
public static class ReminderSchedule
{
    public const int DueSoonKm = 1_000;
    public const int DueSoonDays = 30;

    /// <exception cref="ArgumentException"><paramref name="intervalKm"/> informado sem <paramref name="lastDoneOdometer"/>.</exception>
    public static ReminderStatus Evaluate(
        int? intervalKm,
        int? intervalMonths,
        DateOnly lastDoneDate,
        int? lastDoneOdometer,
        int currentOdometer,
        DateOnly today)
    {
        int? kmRemaining = null;
        int? daysRemaining = null;
        var state = ReminderState.Ok;

        if (intervalKm is { } km)
        {
            if (lastDoneOdometer is not { } baseKm)
                throw new ArgumentException(
                    "Um lembrete por km exige a km da última vez.", nameof(lastDoneOdometer));

            kmRemaining = baseKm + km - currentOdometer;
            state = Worst(state, AxisState(kmRemaining.Value, DueSoonKm));
        }

        if (intervalMonths is { } months)
        {
            daysRemaining = lastDoneDate.AddMonths(months).DayNumber - today.DayNumber;
            state = Worst(state, AxisState(daysRemaining.Value, DueSoonDays));
        }

        return new ReminderStatus(state, kmRemaining, daysRemaining);
    }

    private static ReminderState AxisState(int remaining, int dueSoonThreshold) =>
        remaining <= 0 ? ReminderState.Overdue
        : remaining <= dueSoonThreshold ? ReminderState.DueSoon
        : ReminderState.Ok;

    private static ReminderState Worst(ReminderState a, ReminderState b) => a > b ? a : b;
}
