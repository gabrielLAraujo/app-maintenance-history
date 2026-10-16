using Garage.Application.Abstractions.Ports;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Demo;

/// <summary>
/// A trava do teto de veículos é do processo: a API roda numa instância só (Coolify). Travas listradas (64) mantêm a
/// memória fixa; duas contas que caem na mesma listra só esperam uma pela outra.
/// </summary>
internal sealed class DemoLimits(IOptions<DemoVisitorOptions> options) : IDemoLimits
{
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public int MaxVehicles => options.Value.MaxVehicles;

    public async Task<IDisposable> EnterVehicleGateAsync(Guid ownerId, CancellationToken ct)
    {
        var gate = _gates[(ownerId.GetHashCode() & int.MaxValue) % _gates.Length];
        await gate.WaitAsync(ct);
        return new Release(gate);
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
