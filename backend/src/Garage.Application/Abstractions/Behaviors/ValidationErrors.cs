using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Garage.Domain.Common;

namespace Garage.Application.Abstractions.Behaviors;

public static class ValidationErrors
{
    /// <summary>
    /// Monta o erro <c>validation.failed</c> com as mensagens agrupadas por campo, em camelCase por segmento
    /// (<c>Parts[0].Name</c> → <c>parts[0].name</c>).
    /// </summary>
    public static Error FromFailures(IEnumerable<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var errors = failures
            .GroupBy(failure => ToFieldPath(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return Error.Validation(errors);
    }

    internal static async Task<Error?> ValidateAsync<T>(
        IEnumerable<IValidator<T>> validators, T instance, CancellationToken ct)
    {
        List<ValidationFailure> failures = [];
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(instance, ct);
            failures.AddRange(result.Errors);
        }

        return failures.Count == 0 ? null : FromFailures(failures);
    }

    private static string ToFieldPath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
