namespace Garage.Domain.Reminders;

/// <summary>Intervalo do lembrete em km, em meses ou ambos (M1 exige pelo menos um).</summary>
public sealed record ReminderInterval(int? Km, int? Months);
