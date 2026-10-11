using FluentValidation;

namespace Garage.Application.Reminders.CreateReminder;

internal sealed class CreateReminderValidator : AbstractValidator<CreateReminderCommand>
{
    public CreateReminderValidator(IValidator<ReminderInput> reminder) =>
        // O corpo HTTP é plano: sem prefixo "reminder", as chaves de erro batem com os campos do formulário.
        RuleFor(x => x.Reminder).SetValidator(reminder).OverridePropertyName(string.Empty);
}
