namespace Garage.Domain.Common;

public sealed record Error(string Code, string Message, ErrorType Type, IReadOnlyDictionary<string, object?>? Metadata = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("validation.failed", "Dados inválidos.", ErrorType.Validation,
            new Dictionary<string, object?> { ["errors"] = fieldErrors });
}
