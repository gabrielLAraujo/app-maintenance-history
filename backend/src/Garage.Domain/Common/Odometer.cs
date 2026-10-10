namespace Garage.Domain.Common;

public readonly record struct Odometer
{
    public const int Max = 2_000_000;

    public Odometer(int km)
    {
        if (!IsValid(km))
            throw new ArgumentOutOfRangeException(nameof(km), km, $"A quilometragem deve estar entre 0 e {Max}.");

        Km = km;
    }

    public int Km { get; }

    public static bool IsValid(int km) => km is >= 0 and <= Max;
}
