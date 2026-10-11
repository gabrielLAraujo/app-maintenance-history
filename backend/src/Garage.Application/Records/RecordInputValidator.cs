using FluentValidation;
using Garage.Application.Abstractions.Validation;
using Garage.Domain.Common;

namespace Garage.Application.Records;

/// <summary>
/// Formato dos dados do registro (obrigatórios, tamanhos, faixas); reaproveitado por criar e atualizar. Também barra os
/// totais (de cada peça e do registro) acima do limite do <see cref="Money"/>, que o domínio não representa: ele lançaria
/// ao calcular. R1–R3 ficam no domínio.
/// </summary>
internal sealed class RecordInputValidator : AbstractValidator<RecordInput>
{
    private const int MaxParts = 50;
    private const decimal MaxQuantity = 99_999.999m;
    private const string MoneyRange = "entre R$ 0,00 e R$ 9.999.999.999,99, com no máximo 2 casas decimais";
    private const string MoneyLimit = "R$ 9.999.999.999,99";

    public RecordInputValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo de registro inválido.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("Informe a data.");

        RuleFor(x => x.Odometer!.Value)
            .MustBeValidOdometer()
            .OverridePropertyName(nameof(RecordInput.Odometer))
            .When(x => x.Odometer is not null);

        RuleFor(x => x.Title).NotEmpty().WithMessage("Informe o título.")
            .MaximumLength(120).WithMessage("O título deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("A descrição deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Categoria inválida.");
        RuleFor(x => x.PerformedBy).MaximumLength(120).WithMessage("Quem fez o serviço deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.ReplacedOriginalPart).MaximumLength(500)
            .WithMessage("A peça original substituída deve ter no máximo {MaxLength} caracteres.");

        RuleFor(x => x.LaborCost!.Value)
            .Must(Money.IsValid).WithMessage($"A mão de obra deve estar {MoneyRange}.")
            .OverridePropertyName(nameof(RecordInput.LaborCost))
            .When(x => x.LaborCost is not null);

        RuleFor(x => x.Parts).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Informe as peças.")
            .Must(parts => parts.Count <= MaxParts).WithMessage($"Informe no máximo {MaxParts} peças.");

        // Sem Cascade(Stop): num RuleForEach ele interromperia a coleção inteira na primeira peça inválida. As regras
        // filhas não rodam para uma peça nula.
        RuleForEach(x => x.Parts)
            .NotNull().WithMessage("Informe os dados da peça.")
            .ChildRules(part =>
            {
                part.RuleFor(p => p.Name).NotEmpty().WithMessage("Informe o nome da peça.")
                    .MaximumLength(120).WithMessage("O nome da peça deve ter no máximo {MaxLength} caracteres.");
                part.RuleFor(p => p.Brand).MaximumLength(60).WithMessage("A marca deve ter no máximo {MaxLength} caracteres.");
                part.RuleFor(p => p.PartNumber).MaximumLength(60)
                    .WithMessage("O código da peça deve ter no máximo {MaxLength} caracteres.");
                part.RuleFor(p => p.Quantity).Must(IsValidQuantity)
                    .WithMessage("A quantidade deve ser maior que zero e de no máximo 99.999,999, com até 3 casas decimais.");
                part.RuleFor(p => p.Unit).IsInEnum().WithMessage("Unidade inválida.");

                part.RuleFor(p => p.UnitPrice!.Value).Cascade(CascadeMode.Stop)
                    .Must(Money.IsValid).WithMessage($"O preço unitário deve estar {MoneyRange}.")
                    .Must((p, unitPrice) => !IsValidQuantity(p.Quantity) || Money.IsValid(LineTotal(p.Quantity, unitPrice)))
                    .WithMessage($"O total da peça (quantidade × preço unitário) passa do limite de {MoneyLimit}.")
                    .OverridePropertyName(nameof(PartInput.UnitPrice))
                    .When(p => p.UnitPrice is not null);
            });

        // Só com todos os valores válidos: senão o erro já está no campo que o causou.
        RuleFor(x => x)
            .Must(input => Money.IsValid(TotalCost(input)))
            .WithMessage($"O custo total do registro (peças + mão de obra) passa do limite de {MoneyLimit}.")
            .OverridePropertyName("TotalCost")
            .When(HasOnlyValidAmounts);
    }

    private static bool IsValidQuantity(decimal quantity) =>
        quantity is > 0m and <= MaxQuantity && decimal.Round(quantity, 3) == quantity;

    // Mesmo arredondamento de Money.Times, sem construir o Money (que lançaria acima do limite).
    private static decimal LineTotal(decimal quantity, decimal unitPrice) =>
        decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);

    private static decimal TotalCost(RecordInput input) =>
        input.Parts.Sum(part => LineTotal(part.Quantity, part.UnitPrice ?? 0m)) + (input.LaborCost ?? 0m);

    private static bool HasOnlyValidAmounts(RecordInput input) =>
        (input.LaborCost is null || Money.IsValid(input.LaborCost.Value))
        && input.Parts is not null
        && input.Parts.All(part =>
            part is not null
            && IsValidQuantity(part.Quantity)
            && (part.UnitPrice is null
                || (Money.IsValid(part.UnitPrice.Value) && Money.IsValid(LineTotal(part.Quantity, part.UnitPrice.Value)))));
}
