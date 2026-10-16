namespace Garage.Application.Abstractions.Ports;

/// <summary>Limites das contas demo (<c>Demo:Visitors</c>).</summary>
public interface IDemoLimits
{
    /// <summary>Quantos veículos uma conta demo pode ter.</summary>
    int MaxVehicles { get; }
}
