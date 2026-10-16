namespace Garage.Application.Abstractions.Ports;

/// <summary>Limites das contas demo (<c>Demo:Visitors</c>).</summary>
public interface IDemoLimits
{
    /// <summary>Quantos veículos uma conta demo pode ter.</summary>
    int MaxVehicles { get; }

    /// <summary>
    /// Trava por conta para conferir o teto de veículos e gravar o novo sem que outro pedido da mesma conta passe no meio.
    /// Quem entra solta a trava (Dispose) depois do commit ou do descarte. <c>null</c> quando a espera passa do limite.
    /// </summary>
    Task<IDisposable?> EnterVehicleGateAsync(Guid ownerId, CancellationToken ct);
}
