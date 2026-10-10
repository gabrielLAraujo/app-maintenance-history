using Garage.Domain.Common;
using Garage.Domain.Reminders;

namespace Garage.Domain.Tests.Reminders;

public class ReminderTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);
    private static readonly DateOnly BaseDate = new(2026, 4, 10);
    private static readonly Guid VehicleId = Guid.Parse("0197b3a0-0000-7000-8000-000000000002");

    private static readonly ReminderInterval KmAndMonths = new(5_000, 6);
    private static readonly ReminderInterval KmOnly = new(5_000, null);
    private static readonly ReminderInterval MonthsOnly = new(null, 6);

    private static Result<Reminder> Create(
        ReminderInterval? interval = null,
        DateOnly? lastDoneDate = null,
        Odometer? lastDoneOdometer = null,
        string title = "Troca de óleo") =>
        Reminder.Create(
            VehicleId,
            title,
            interval ?? KmAndMonths,
            lastDoneDate ?? BaseDate,
            lastDoneOdometer ?? new Odometer(80_000),
            Today);

    private static Reminder NewReminder(ReminderInterval? interval = null) =>
        interval is { Km: null }
            ? Reminder.Create(VehicleId, "Revisão geral", interval, BaseDate, null, Today).Value
            : Create(interval).Value;

    [Fact]
    public void Create_populates_fields_and_generates_a_version7_id()
    {
        var reminder = Create().Value;

        Assert.Equal(7, reminder.Id.Version);
        Assert.Equal(VehicleId, reminder.VehicleId);
        Assert.Equal("Troca de óleo", reminder.Title);
        Assert.Equal(5_000, reminder.IntervalKm);
        Assert.Equal(6, reminder.IntervalMonths);
        Assert.Equal(BaseDate, reminder.LastDoneDate);
        Assert.Equal(80_000, reminder.LastDoneOdometer!.Value.Km);
    }

    [Fact]
    public void Create_months_only_accepts_a_missing_odometer()
    {
        var result = Reminder.Create(VehicleId, "Revisão geral", MonthsOnly, BaseDate, null, Today);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.IntervalKm);
        Assert.Equal(6, result.Value.IntervalMonths);
        Assert.Null(result.Value.LastDoneOdometer);
    }

    [Fact]
    public void Create_km_only_has_no_months()
    {
        var result = Create(KmOnly);

        Assert.True(result.IsSuccess);
        Assert.Equal(5_000, result.Value.IntervalKm);
        Assert.Null(result.Value.IntervalMonths);
    }

    [Fact]
    public void Create_without_interval_fails()
    {
        var result = Reminder.Create(VehicleId, "Troca de óleo", new ReminderInterval(null, null), BaseDate, null, Today);

        Assert.True(result.IsFailure);
        Assert.Equal("reminder.interval_required", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Create_with_base_date_in_future_fails()
    {
        var result = Create(lastDoneDate: new DateOnly(2026, 10, 11));

        Assert.True(result.IsFailure);
        Assert.Equal("reminder.base_date_in_future", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
    }

    [Fact]
    public void Create_with_base_date_today_succeeds()
    {
        var result = Create(lastDoneDate: Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(Today, result.Value.LastDoneDate);
    }

    [Fact]
    public void Create_validates_M1_then_M5()
    {
        var result = Reminder.Create(
            VehicleId, "Troca de óleo", new ReminderInterval(null, null), new DateOnly(2026, 10, 11), null, Today);

        Assert.Equal("reminder.interval_required", result.Error!.Code);
    }

    [Fact]
    public void Create_with_km_interval_and_no_base_odometer_throws()
    {
        Assert.Throws<ArgumentException>(
            () => Reminder.Create(VehicleId, "Troca de óleo", KmOnly, BaseDate, null, Today));
    }

    [Fact]
    public void Update_replaces_fields()
    {
        var reminder = NewReminder();
        var id = reminder.Id;

        var result = reminder.Update("Troca do filtro", MonthsOnly, new DateOnly(2026, 6, 1), null, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, reminder.Id);
        Assert.Equal(VehicleId, reminder.VehicleId);
        Assert.Equal("Troca do filtro", reminder.Title);
        Assert.Null(reminder.IntervalKm);
        Assert.Equal(6, reminder.IntervalMonths);
        Assert.Equal(new DateOnly(2026, 6, 1), reminder.LastDoneDate);
        Assert.Null(reminder.LastDoneOdometer);
    }

    [Fact]
    public void Update_can_move_base_backwards()
    {
        var reminder = NewReminder();

        var result = reminder.Update("Troca de óleo", KmAndMonths, new DateOnly(2026, 1, 15), new Odometer(75_000), Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 1, 15), reminder.LastDoneDate);
        Assert.Equal(75_000, reminder.LastDoneOdometer!.Value.Km);
    }

    [Fact]
    public void Update_without_interval_fails_and_keeps_state()
    {
        var reminder = NewReminder();

        var result = reminder.Update("Outro título", new ReminderInterval(null, null), new DateOnly(2026, 6, 1), null, Today);

        Assert.True(result.IsFailure);
        Assert.Equal("reminder.interval_required", result.Error!.Code);
        AssertUnchanged(reminder);
    }

    [Fact]
    public void Update_with_base_date_in_future_fails_and_keeps_state()
    {
        var reminder = NewReminder();

        var result = reminder.Update("Outro título", MonthsOnly, new DateOnly(2026, 10, 11), null, Today);

        Assert.True(result.IsFailure);
        Assert.Equal("reminder.base_date_in_future", result.Error!.Code);
        AssertUnchanged(reminder);
    }

    [Fact]
    public void Update_validates_M1_then_M5()
    {
        var reminder = NewReminder();

        var result = reminder.Update("Outro título", new ReminderInterval(null, null), new DateOnly(2026, 10, 11), null, Today);

        Assert.Equal("reminder.interval_required", result.Error!.Code);
    }

    [Fact]
    public void Update_with_km_interval_and_no_base_odometer_throws_and_keeps_state()
    {
        var reminder = NewReminder();

        Assert.Throws<ArgumentException>(() => reminder.Update("Outro título", KmOnly, new DateOnly(2026, 6, 1), null, Today));
        AssertUnchanged(reminder);
    }

    [Fact]
    public void MarkDone_moves_base()
    {
        var reminder = NewReminder();

        var result = reminder.MarkDone(new DateOnly(2026, 10, 1), new Odometer(85_000));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 10, 1), reminder.LastDoneDate);
        Assert.Equal(85_000, reminder.LastDoneOdometer!.Value.Km);
        Assert.Equal(5_000, reminder.IntervalKm);
        Assert.Equal(6, reminder.IntervalMonths);
    }

    [Fact]
    public void MarkDone_same_date_succeeds()
    {
        var reminder = NewReminder();

        var result = reminder.MarkDone(BaseDate, new Odometer(80_500));

        Assert.True(result.IsSuccess);
        Assert.Equal(BaseDate, reminder.LastDoneDate);
        Assert.Equal(80_500, reminder.LastDoneOdometer!.Value.Km);
    }

    [Fact]
    public void MarkDone_older_than_base_fails_and_keeps_state()
    {
        var reminder = NewReminder();

        var result = reminder.MarkDone(new DateOnly(2026, 4, 9), new Odometer(85_000));

        Assert.True(result.IsFailure);
        Assert.Equal("reminder.fulfillment_older_than_base", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
        AssertUnchanged(reminder);
    }

    [Fact]
    public void MarkDone_without_odometer_on_km_reminder_fails_and_keeps_state()
    {
        var reminder = NewReminder();

        var result = reminder.MarkDone(new DateOnly(2026, 10, 1), null);

        Assert.True(result.IsFailure);
        Assert.Equal("reminder.fulfillment_requires_odometer", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
        AssertUnchanged(reminder);
    }

    [Fact]
    public void MarkDone_checks_M3_before_M4()
    {
        var reminder = NewReminder();

        var result = reminder.MarkDone(new DateOnly(2026, 4, 9), null);

        Assert.Equal("reminder.fulfillment_requires_odometer", result.Error!.Code);
    }

    [Fact]
    public void MarkDone_without_odometer_on_months_only_succeeds()
    {
        var reminder = NewReminder(MonthsOnly);

        var result = reminder.MarkDone(new DateOnly(2026, 10, 1), null);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 10, 1), reminder.LastDoneDate);
        Assert.Null(reminder.LastDoneOdometer);
    }

    [Fact]
    public void MarkDone_with_odometer_on_months_only_records_it()
    {
        var reminder = NewReminder(MonthsOnly);

        var result = reminder.MarkDone(new DateOnly(2026, 10, 1), new Odometer(85_000));

        Assert.True(result.IsSuccess);
        Assert.Equal(85_000, reminder.LastDoneOdometer!.Value.Km);
    }

    [Fact]
    public void MarkDone_restarts_the_status()
    {
        var reminder = NewReminder();
        Assert.Equal(ReminderState.Overdue, reminder.Evaluate(new Odometer(86_000), Today).State);

        reminder.MarkDone(Today, new Odometer(86_000));

        var status = reminder.Evaluate(new Odometer(86_000), Today);
        Assert.Equal(ReminderState.Ok, status.State);
        Assert.Equal(5_000, status.KmRemaining);
        Assert.Equal(182, status.DaysRemaining);
    }

    [Fact]
    public void Evaluate_delegates_to_the_schedule()
    {
        var reminder = NewReminder();

        var status = reminder.Evaluate(new Odometer(84_500), new DateOnly(2026, 5, 1));

        var expected = ReminderSchedule.Evaluate(5_000, 6, BaseDate, 80_000, 84_500, new DateOnly(2026, 5, 1));
        Assert.Equal(expected, status);
        Assert.Equal(ReminderState.DueSoon, status.State);
        Assert.Equal(500, status.KmRemaining);
        Assert.Equal(162, status.DaysRemaining);
    }

    [Fact]
    public void Evaluate_months_only_ignores_the_odometer()
    {
        var reminder = NewReminder(MonthsOnly);

        var status = reminder.Evaluate(new Odometer(1_000_000), Today);

        Assert.Equal(ReminderState.Overdue, status.State);
        Assert.Null(status.KmRemaining);
        Assert.Equal(0, status.DaysRemaining);
    }

    private static void AssertUnchanged(Reminder reminder)
    {
        Assert.Equal("Troca de óleo", reminder.Title);
        Assert.Equal(5_000, reminder.IntervalKm);
        Assert.Equal(6, reminder.IntervalMonths);
        Assert.Equal(BaseDate, reminder.LastDoneDate);
        Assert.Equal(80_000, reminder.LastDoneOdometer!.Value.Km);
    }
}

public class ReminderErrorsTests
{
    [Fact]
    public void NotFound_is_a_not_found_error_in_pt_BR()
    {
        var error = ReminderErrors.NotFound;

        Assert.Equal("reminder.not_found", error.Code);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("Lembrete não encontrado.", error.Message);
    }

    [Fact]
    public void IntervalRequired_is_a_domain_rule_error_in_pt_BR()
    {
        var error = ReminderErrors.IntervalRequired;

        Assert.Equal("reminder.interval_required", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("Informe o intervalo em km, em meses ou ambos.", error.Message);
    }

    [Fact]
    public void FulfillmentRequiresOdometer_is_a_domain_rule_error_in_pt_BR()
    {
        var error = ReminderErrors.FulfillmentRequiresOdometer;

        Assert.Equal("reminder.fulfillment_requires_odometer", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("Este lembrete é por km: informe a km do registro.", error.Message);
    }

    [Fact]
    public void FulfillmentOlderThanBase_is_a_domain_rule_error_in_pt_BR()
    {
        var error = ReminderErrors.FulfillmentOlderThanBase;

        Assert.Equal("reminder.fulfillment_older_than_base", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("O registro é anterior à última vez que este lembrete foi feito.", error.Message);
    }

    [Fact]
    public void BaseDateInFuture_is_a_domain_rule_error_in_pt_BR()
    {
        var error = ReminderErrors.BaseDateInFuture;

        Assert.Equal("reminder.base_date_in_future", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("A data da última vez não pode estar no futuro.", error.Message);
    }
}
