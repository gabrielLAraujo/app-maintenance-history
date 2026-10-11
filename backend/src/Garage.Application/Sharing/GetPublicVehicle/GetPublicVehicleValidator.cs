using FluentValidation;
using Garage.Application.Records;

namespace Garage.Application.Sharing.GetPublicVehicle;

internal sealed class GetPublicVehicleValidator : AbstractValidator<GetPublicVehicleQuery>
{
    // Só o cursor: o token não é validado aqui, porque um token inválido responde 404 como um inexistente (S1).
    public GetPublicVehicleValidator() =>
        RuleFor(x => x.Cursor)
            .Must(cursor => RecordCursor.TryDecode(cursor, out _)).WithMessage("Cursor inválido.")
            .When(x => !string.IsNullOrEmpty(x.Cursor));
}
