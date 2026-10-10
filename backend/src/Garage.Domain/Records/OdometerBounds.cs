namespace Garage.Domain.Records;

/// <summary>
/// Limites de km impostos pela cronologia do histórico (R3): a maior km entre os registros estritamente
/// anteriores e a menor km entre os registros estritamente posteriores. Nulo = sem limite naquele lado.
/// </summary>
public readonly record struct OdometerBounds(int? MaxKmBefore, int? MinKmAfter)
{
    public static OdometerBounds None { get; } = new(null, null);
}
