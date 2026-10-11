using System.Globalization;
using FluentValidation;
using Garage.Domain.Common;

namespace Garage.Application.Abstractions.Validation;

internal static class OdometerValidation
{
    private static readonly string Message =
        $"A quilometragem deve estar entre 0 e {Odometer.Max.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"))} km.";

    /// <summary>A km precisa estar no intervalo de <see cref="Odometer"/> (o domínio lança se receber um valor fora dele).</summary>
    public static IRuleBuilderOptions<T, int> MustBeValidOdometer<T>(this IRuleBuilder<T, int> rule) =>
        rule.Must(Odometer.IsValid).WithMessage(Message);
}
