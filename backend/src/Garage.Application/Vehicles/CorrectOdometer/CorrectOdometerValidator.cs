using FluentValidation;
using Garage.Application.Abstractions.Validation;

namespace Garage.Application.Vehicles.CorrectOdometer;

internal sealed class CorrectOdometerValidator : AbstractValidator<CorrectOdometerCommand>
{
    public CorrectOdometerValidator() => RuleFor(x => x.Km).MustBeValidOdometer();
}
