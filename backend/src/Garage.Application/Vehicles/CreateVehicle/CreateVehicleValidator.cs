using FluentValidation;
using Garage.Application.Abstractions.Validation;

namespace Garage.Application.Vehicles.CreateVehicle;

internal sealed class CreateVehicleValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleValidator(IValidator<VehicleInput> vehicle)
    {
        // O corpo HTTP é plano: sem prefixo "vehicle", as chaves de erro batem com os campos do formulário.
        RuleFor(x => x.Vehicle).SetValidator(vehicle).OverridePropertyName(string.Empty);
        RuleFor(x => x.CurrentOdometer).MustBeValidOdometer();
    }
}
