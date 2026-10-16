using Garage.Application.Abstractions.Ports;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Demo;

/// <summary>
/// A trava do teto de veículos é do processo: a API roda numa instância só (Coolify). Travas listradas (64) mantêm a
/// memória fixa; duas contas que caem na mesma listra só esperam uma pela outra. A espera tem limite (10 s): uma trava
/// que nunca fosse solta não prende as contas daquela listra até a API reiniciar.
/// </summary>
internal sealed class DemoLimits : IDemoLimits
{
    private readonly IOptions<DemoVisitorOptions> _options;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public DemoLimits(IOptions<DemoVisitorOptions> options)
        : this(options, TimeSpan.FromSeconds(10))
    {
    }

    internal DemoLimits(IOptions<DemoVisitorOptions> options, TimeSpan timeout)
    {
        _options = options;
        _timeout = timeout;
    }

    public int MaxVehicles => _options.Value.MaxVehicles;

    public async Task<IDisposable?> EnterVehicleGateAsync(Guid ownerId, CancellationToken ct)
    {
        var gate = _gates[(ownerId.GetHashCode() & int.MaxValue) % _gates.Length];
        return await gate.WaitAsync(_timeout, ct) ? new Release(gate) : null;
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                gate.Release();
        }
    }
}
