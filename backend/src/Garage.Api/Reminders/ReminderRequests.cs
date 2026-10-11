using Garage.Application.Reminders;
using Garage.Application.Reminders.CreateReminder;
using Garage.Application.Reminders.UpdateReminder;

namespace Garage.Api.Reminders;

// Os corpos são planos (os campos do lembrete no nível raiz), então as chaves de validation.failed
// batem com os campos do formulário; cada request monta o comando com um ReminderInput.

public sealed record CreateReminderRequest(
    string Title,
    int? IntervalKm,
    int? IntervalMonths,
    DateOnly LastDoneDate,
    int? LastDoneOdometer)
{
    internal CreateReminderCommand ToCommand(Guid vehicleId) => new(
        vehicleId,
        new ReminderInput(Title, IntervalKm, IntervalMonths, LastDoneDate, LastDoneOdometer));
}

public sealed record UpdateReminderRequest(
    string Title,
    int? IntervalKm,
    int? IntervalMonths,
    DateOnly LastDoneDate,
    int? LastDoneOdometer,
    uint RowVersion)
{
    internal UpdateReminderCommand ToCommand(Guid vehicleId, Guid reminderId) => new(
        vehicleId,
        reminderId,
        new ReminderInput(Title, IntervalKm, IntervalMonths, LastDoneDate, LastDoneOdometer),
        RowVersion);
}
