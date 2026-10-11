using FluentValidation;
using Garage.Application.Abstractions.Validation;

namespace Garage.Application.Vehicles.UpdateOdometer;

internal sealed class UpdateOdometerValidator : AbstractValidator<UpdateOdometerCommand>
{
    public UpdateOdometerValidator() => RuleFor(x => x.Km).MustBeValidOdometer();
}
