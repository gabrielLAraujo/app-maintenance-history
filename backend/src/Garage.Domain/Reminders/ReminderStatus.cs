namespace Garage.Domain.Reminders;

/// <summary>Resultado do cálculo; o restante de um eixo ausente é nulo e pode ser negativo quando vencido.</summary>
public sealed record ReminderStatus(ReminderState State, int? KmRemaining, int? DaysRemaining);
