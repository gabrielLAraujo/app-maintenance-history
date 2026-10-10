namespace Garage.Domain.Reminders;

/// <summary>Estado de um lembrete; a ordem dos valores vai do melhor ao pior (usada para escolher o pior eixo).</summary>
public enum ReminderState
{
    Ok,
    DueSoon,
    Overdue
}
