using System.Text.RegularExpressions;

namespace Garage.Domain.Vehicles;

public sealed partial record Plate
{
    private Plate(string value) => Value = value;

    public string Value { get; }

    public static string Normalize(string raw) =>
        raw.Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

    public static bool IsValid(string raw) => PlatePattern().IsMatch(Normalize(raw));

    public static Plate From(string raw)
    {
        if (!IsValid(raw))
            throw new ArgumentException("Placa inválida.", nameof(raw));

        return new Plate(Normalize(raw));
    }

    // \z (e não $) para que uma quebra de linha final não passe como válida.
    [GeneratedRegex(@"^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}\z")]
    private static partial Regex PlatePattern();
}
