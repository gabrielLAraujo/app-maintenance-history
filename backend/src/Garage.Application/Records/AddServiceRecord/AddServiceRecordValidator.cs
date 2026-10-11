using FluentValidation;

namespace Garage.Application.Records.AddServiceRecord;

internal sealed class AddServiceRecordValidator : AbstractValidator<AddServiceRecordCommand>
{
    private const int MaxFulfilledReminders = 20;

    public AddServiceRecordValidator(IValidator<RecordInput> record)
    {
        // O corpo HTTP é plano: sem prefixo "record", as chaves de erro batem com os campos do formulário.
        RuleFor(x => x.Record).SetValidator(record).OverridePropertyName(string.Empty);

        RuleFor(x => x.FulfilledReminderIds).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Informe os lembretes cumpridos.")
            .Must(ids => ids.Count <= MaxFulfilledReminders)
            .WithMessage($"Informe no máximo {MaxFulfilledReminders} lembretes cumpridos.");
    }
}
