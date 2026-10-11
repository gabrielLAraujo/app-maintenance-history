using FluentValidation;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Vehicles;

namespace Garage.Application.Vehicles;

/// <summary>
/// Formato dos dados do veículo (obrigatórios, tamanhos, valores válidos); reaproveitado por criar e atualizar.
/// As invariantes de negócio ficam no domínio.
/// </summary>
internal sealed class VehicleInputValidator : AbstractValidator<VehicleInput>
{
    public VehicleInputValidator(IAppClock clock)
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo de veículo inválido.");
        RuleFor(x => x.Make).NotEmpty().WithMessage("Informe a marca.")
            .MaximumLength(60).WithMessage("A marca deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.Model).NotEmpty().WithMessage("Informe o modelo.")
            .MaximumLength(60).WithMessage("O modelo deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.Version).MaximumLength(120).WithMessage("A versão deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.Generation).MaximumLength(30).WithMessage("A geração deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.Fuel).IsInEnum().WithMessage("Combustível inválido.");
        RuleFor(x => x.Nickname).MaximumLength(60).WithMessage("O apelido deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.Vin).MaximumLength(17).WithMessage("O chassi deve ter no máximo {MaxLength} caracteres.");
        RuleFor(x => x.Notes).MaximumLength(2000).WithMessage("As observações devem ter no máximo {MaxLength} caracteres.");

        // Placa em branco conta como ausente (ver VehicleInputMapping).
        RuleFor(x => x.Plate).Must(Plate.IsValid).WithMessage("Placa inválida.")
            .When(x => !string.IsNullOrWhiteSpace(x.Plate));

        // Um único erro, na chave modelYear, cobre fabricação e modelo: o formulário trata os dois anos juntos.
        RuleFor(x => x.ModelYear)
            .Must((input, _) => VehicleYear.IsValid(input.ManufactureYear, input.ModelYear, clock.CurrentYear))
            .WithMessage("Ano inválido.");

        RuleFor(x => x.Catalog!).ChildRules(catalog =>
        {
            catalog.RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo de veículo inválido.");
            catalog.RuleFor(x => x.FipeCode).NotEmpty().WithMessage("Informe o código FIPE.")
                .MaximumLength(20).WithMessage("O código FIPE deve ter no máximo {MaxLength} caracteres.");
            catalog.RuleFor(x => x.FipeYearCode).NotEmpty().WithMessage("Informe o código do ano FIPE.")
                .MaximumLength(20).WithMessage("O código do ano FIPE deve ter no máximo {MaxLength} caracteres.");
        }).When(x => x.Catalog is not null);
    }
}
