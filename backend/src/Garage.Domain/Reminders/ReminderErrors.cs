using Garage.Domain.Common;

namespace Garage.Domain.Reminders;

public static class ReminderErrors
{
    public static readonly Error NotFound = new(
        "reminder.not_found",
        "Lembrete não encontrado.",
        ErrorType.NotFound);

    public static readonly Error IntervalRequired = new(
        "reminder.interval_required",
        "Informe o intervalo em km, em meses ou ambos.",
        ErrorType.DomainRule);

    public static readonly Error FulfillmentRequiresOdometer = new(
        "reminder.fulfillment_requires_odometer",
        "Este lembrete é por km: informe a km do registro.",
        ErrorType.DomainRule);

    public static readonly Error FulfillmentOlderThanBase = new(
        "reminder.fulfillment_older_than_base",
        "O registro é anterior à última vez que este lembrete foi feito.",
        ErrorType.DomainRule);

    public static readonly Error BaseDateInFuture = new(
        "reminder.base_date_in_future",
        "A data da última vez não pode estar no futuro.",
        ErrorType.DomainRule);
}
