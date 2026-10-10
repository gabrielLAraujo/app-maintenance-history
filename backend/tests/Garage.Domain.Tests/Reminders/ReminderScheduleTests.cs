using Garage.Domain.Reminders;

namespace Garage.Domain.Tests.Reminders;

public class ReminderScheduleTests
{
    private static readonly DateOnly BaseDate = new(2026, 4, 10);

    private static ReminderStatus Evaluate(
        int? intervalKm,
        int? intervalMonths,
        int? lastDoneOdometer,
        int currentOdometer,
        DateOnly today) =>
        ReminderSchedule.Evaluate(intervalKm, intervalMonths, BaseDate, lastDoneOdometer, currentOdometer, today);

    [Fact]
    public void Thresholds_are_one_thousand_km_and_thirty_days()
    {
        Assert.Equal(1_000, ReminderSchedule.DueSoonKm);
        Assert.Equal(30, ReminderSchedule.DueSoonDays);
    }

    // km: intervalo 5_000, base 80_000 (vence em 85_000)
    [Theory]
    [InlineData(83_999, ReminderState.Ok, 1_001)]
    [InlineData(84_000, ReminderState.DueSoon, 1_000)]
    [InlineData(84_999, ReminderState.DueSoon, 1)]
    [InlineData(85_000, ReminderState.Overdue, 0)]
    [InlineData(86_000, ReminderState.Overdue, -1_000)]
    public void Km_axis_boundaries(int current, ReminderState state, int kmRemaining)
    {
        var status = Evaluate(5_000, null, 80_000, current, new DateOnly(2026, 10, 10));

        Assert.Equal(state, status.State);
        Assert.Equal(kmRemaining, status.KmRemaining);
        Assert.Null(status.DaysRemaining);
    }

    // meses: intervalo 6, base 2026-04-10 (vence em 2026-10-10)
    [Theory]
    [InlineData("2026-09-09", ReminderState.Ok, 31)]
    [InlineData("2026-09-10", ReminderState.DueSoon, 30)]
    [InlineData("2026-10-09", ReminderState.DueSoon, 1)]
    [InlineData("2026-10-10", ReminderState.Overdue, 0)]
    [InlineData("2026-10-20", ReminderState.Overdue, -10)]
    public void Months_axis_boundaries(string today, ReminderState state, int daysRemaining)
    {
        var status = Evaluate(null, 6, null, 0, DateOnly.ParseExact(today, "yyyy-MM-dd"));

        Assert.Equal(state, status.State);
        Assert.Equal(daysRemaining, status.DaysRemaining);
        Assert.Null(status.KmRemaining);
    }

    [Fact]
    public void Whichever_comes_first_by_time()
    {
        // 5_000 km / 6 meses, base 80_000 / 2026-04-10: a km ainda sobra, mas o prazo venceu hoje.
        var status = Evaluate(5_000, 6, 80_000, 81_000, new DateOnly(2026, 10, 10));

        Assert.Equal(ReminderState.Overdue, status.State);
        Assert.Equal(4_000, status.KmRemaining);
        Assert.Equal(0, status.DaysRemaining);
    }

    [Fact]
    public void Whichever_comes_first_by_km()
    {
        // O prazo ainda está longe (162 dias), mas faltam só 500 km.
        var status = Evaluate(5_000, 6, 80_000, 84_500, new DateOnly(2026, 5, 1));

        Assert.Equal(ReminderState.DueSoon, status.State);
        Assert.Equal(500, status.KmRemaining);
        Assert.Equal(162, status.DaysRemaining);
    }

    [Theory]
    [InlineData(84_500, "2026-10-10", ReminderState.Overdue)] // km próximo, prazo vencido
    [InlineData(85_500, "2026-05-01", ReminderState.Overdue)] // km vencido, prazo em dia
    [InlineData(84_500, "2026-09-20", ReminderState.DueSoon)] // os dois próximos
    [InlineData(81_000, "2026-09-20", ReminderState.DueSoon)] // km em dia, prazo próximo
    [InlineData(81_000, "2026-05-01", ReminderState.Ok)] // os dois em dia
    public void Final_state_is_the_worst_present_axis(int current, string today, ReminderState expected)
    {
        var status = Evaluate(5_000, 6, 80_000, current, DateOnly.ParseExact(today, "yyyy-MM-dd"));

        Assert.Equal(expected, status.State);
    }

    [Fact]
    public void Months_use_calendar_arithmetic_and_clamp_to_the_end_of_the_month()
    {
        // 2026-08-31 + 6 meses = 2027-02-28
        var status = ReminderSchedule.Evaluate(
            null, 6, new DateOnly(2026, 8, 31), null, 0, new DateOnly(2027, 2, 27));

        Assert.Equal(1, status.DaysRemaining);
        Assert.Equal(ReminderState.DueSoon, status.State);
    }

    [Fact]
    public void Km_axis_with_missing_base_odometer_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => ReminderSchedule.Evaluate(5_000, null, BaseDate, null, 80_000, new DateOnly(2026, 10, 10)));
    }
}
