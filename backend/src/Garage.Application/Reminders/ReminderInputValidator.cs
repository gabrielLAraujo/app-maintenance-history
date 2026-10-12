using FluentValidation;
using Garage.Application.Abstractions.Validation;

namespace Garage.Application.Reminders;

/// <summary>
/// Formato dos dados do lembrete (obrigatórios, tamanhos, faixas); reaproveitado por criar e atualizar.
/// "Ao menos um intervalo" (M1) e "base não pode estar no futuro" (M5) ficam no domínio.
/// </summary>
internal sealed class ReminderInputValidator : AbstractValidator<ReminderInput>
{
    private const int MaxIntervalKm = 1_000_000;
    private const int MaxIntervalMonths = 240;

    public ReminderInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Informe o título.")
            .MaximumLength(120).WithMessage("O título deve ter no máximo {MaxLength} caracteres.");

        RuleFor(x => x.IntervalKm!.Value)
            .InclusiveBetween(1, MaxIntervalKm).WithMessage("O intervalo em km deve estar entre 1 e 1.000.000.")
            .OverridePropertyName(nameof(ReminderInput.IntervalKm))
            .When(x => x.IntervalKm is not null);

        RuleFor(x => x.IntervalMonths!.Value)
            .InclusiveBetween(1, MaxIntervalMonths).WithMessage("O intervalo em meses deve estar entre 1 e 240.")
            .OverridePropertyName(nameof(ReminderInput.IntervalMonths))
            .When(x => x.IntervalMonths is not null);

        // Sem o campo no JSON a data chega como 0001-01-01 (o padrão de DateOnly), e o lembrete nasceria vencido.
        RuleFor(x => x.LastDoneDate).NotEmpty().WithMessage("Informe a data da última vez.");

        // Sem a km da última vez não há como calcular o que falta para um lembrete por km (o domínio lança nesse caso).
        RuleFor(x => x.LastDoneOdometer)
            .NotNull().WithMessage("Informe a km da última vez para lembretes por km.")
            .When(x => x.IntervalKm is not null);

        RuleFor(x => x.LastDoneOdometer!.Value)
            .MustBeValidOdometer()
            .OverridePropertyName(nameof(ReminderInput.LastDoneOdometer))
            .When(x => x.LastDoneOdometer is not null);
    }
}
