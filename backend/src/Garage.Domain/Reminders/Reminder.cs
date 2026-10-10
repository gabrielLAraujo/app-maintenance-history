using Garage.Domain.Common;

namespace Garage.Domain.Reminders;

public sealed class Reminder : AggregateRoot
{
    // Construtor sem parâmetros apenas para a materialização do EF Core; os inicializadores
    // `null!` satisfazem a análise de nulos e são sempre sobrescritos na materialização.
    private Reminder()
    {
    }

    public Guid VehicleId { get; private set; }

    public string Title { get; private set; } = null!;

    public int? IntervalKm { get; private set; }

    public int? IntervalMonths { get; private set; }

    public DateOnly LastDoneDate { get; private set; }

    public Odometer? LastDoneOdometer { get; private set; }

    /// <summary>Valida M1 e M5 nesta ordem; a base em km é obrigatória quando há intervalo em km.</summary>
    /// <exception cref="ArgumentException"><see cref="ReminderInterval.Km"/> informado sem <paramref name="lastDoneOdometer"/>.</exception>
    public static Result<Reminder> Create(
        Guid vehicleId,
        string title,
        ReminderInterval interval,
        DateOnly lastDoneDate,
        Odometer? lastDoneOdometer,
        DateOnly today)
    {
        var validation = Validate(interval, lastDoneDate, lastDoneOdometer, today);
        if (validation.IsFailure)
            return validation.Error!;

        var reminder = new Reminder
        {
            Id = Guid.CreateVersion7(),
            VehicleId = vehicleId
        };

        reminder.Apply(title, interval, lastDoneDate, lastDoneOdometer);
        return reminder;
    }

    /// <summary>Mesmas regras do Create; a base pode voltar no tempo e, em caso de falha, o estado não muda.</summary>
    /// <exception cref="ArgumentException"><see cref="ReminderInterval.Km"/> informado sem <paramref name="lastDoneOdometer"/>.</exception>
    public Result Update(
        string title,
        ReminderInterval interval,
        DateOnly lastDoneDate,
        Odometer? lastDoneOdometer,
        DateOnly today)
    {
        var validation = Validate(interval, lastDoneDate, lastDoneOdometer, today);
        if (validation.IsFailure)
            return validation;

        Apply(title, interval, lastDoneDate, lastDoneOdometer);
        return Result.Success();
    }

    /// <summary>
    /// Cumprimento: M3 (lembrete por km exige km) e M4 (a base só anda para frente), nesta ordem.
    /// No sucesso a base passa a ser <paramref name="date"/> e <paramref name="odometer"/>.
    /// </summary>
    public Result MarkDone(DateOnly date, Odometer? odometer)
    {
        if (IntervalKm is not null && odometer is null)
            return ReminderErrors.FulfillmentRequiresOdometer;

        if (date < LastDoneDate)
            return ReminderErrors.FulfillmentOlderThanBase;

        LastDoneDate = date;
        LastDoneOdometer = odometer;
        return Result.Success();
    }

    public ReminderStatus Evaluate(Odometer current, DateOnly today) =>
        ReminderSchedule.Evaluate(
            IntervalKm,
            IntervalMonths,
            LastDoneDate,
            LastDoneOdometer?.Km,
            current.Km,
            today);

    private static Result Validate(
        ReminderInterval interval,
        DateOnly lastDoneDate,
        Odometer? lastDoneOdometer,
        DateOnly today)
    {
        if (interval.Km is not null && lastDoneOdometer is null)
            throw new ArgumentException(
                "Um lembrete por km exige a km da última vez.", nameof(lastDoneOdometer));

        if (interval.Km is null && interval.Months is null)
            return ReminderErrors.IntervalRequired;

        if (lastDoneDate > today)
            return ReminderErrors.BaseDateInFuture;

        return Result.Success();
    }

    private void Apply(string title, ReminderInterval interval, DateOnly lastDoneDate, Odometer? lastDoneOdometer)
    {
        Title = title;
        IntervalKm = interval.Km;
        IntervalMonths = interval.Months;
        LastDoneDate = lastDoneDate;
        LastDoneOdometer = lastDoneOdometer;
    }
}
