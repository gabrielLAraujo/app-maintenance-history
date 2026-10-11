using FluentValidation;

namespace Garage.Application.Vehicles.UpdateVehicle;

internal sealed class UpdateVehicleValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleValidator(IValidator<VehicleInput> vehicle) =>
        // O corpo HTTP é plano: sem prefixo "vehicle", as chaves de erro batem com os campos do formulário.
        RuleFor(x => x.Vehicle).SetValidator(vehicle).OverridePropertyName(string.Empty);
}
