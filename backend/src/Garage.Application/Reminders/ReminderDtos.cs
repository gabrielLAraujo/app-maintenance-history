using Garage.Domain.Reminders;

namespace Garage.Application.Reminders;

public sealed record ReminderInput(
    string Title,
    int? IntervalKm,
    int? IntervalMonths,
    DateOnly LastDoneDate,
    int? LastDoneOdometer);

/// <summary>
/// Status calculado na leitura (M2). <see cref="OdometerUpdatedAt"/> é a data da km atual do veículo, para a UI avisar
/// quando ela estiver desatualizada.
/// </summary>
public sealed record ReminderStatusDto(
    ReminderState State,
    int? KmRemaining,
    int? DaysRemaining,
    DateTimeOffset OdometerUpdatedAt);

public sealed record ReminderDto(
    Guid Id,
    string Title,
    int? IntervalKm,
    int? IntervalMonths,
    DateOnly LastDoneDate,
    int? LastDoneOdometer,
    ReminderStatusDto Status,
    uint RowVersion);

public sealed record DueReminderDto(
    Guid VehicleId,
    string VehicleName,
    Guid ReminderId,
    string Title,
    ReminderStatusDto Status);
