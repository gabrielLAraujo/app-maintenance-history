using Garage.Infrastructure.Demo;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Tests.Demo;

public sealed class DemoLimitsTests
{
    private static DemoLimits Limits(TimeSpan timeout) =>
        new(Options.Create(new DemoVisitorOptions { MaxVehicles = 5 }), timeout);

    [Fact]
    public async Task Gate_of_the_same_account_waits_and_gives_up_after_the_timeout()
    {
        var limits = Limits(TimeSpan.FromMilliseconds(50));
        var owner = Guid.NewGuid();

        using var held = await limits.EnterVehicleGateAsync(owner, CancellationToken.None);
        var waiting = await limits.EnterVehicleGateAsync(owner, CancellationToken.None);

        // Uma trava que nunca fosse solta (um caminho sem o decorator de transação) não prende a conta para sempre.
        Assert.NotNull(held);
        Assert.Null(waiting);
    }

    [Fact]
    public async Task Gate_is_free_again_after_release()
    {
        var limits = Limits(TimeSpan.FromMilliseconds(50));
        var owner = Guid.NewGuid();

        var first = await limits.EnterVehicleGateAsync(owner, CancellationToken.None);
        first!.Dispose();
        first.Dispose(); // soltar duas vezes não libera uma vaga a mais
        using var second = await limits.EnterVehicleGateAsync(owner, CancellationToken.None);
        var third = await limits.EnterVehicleGateAsync(owner, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Null(third);
    }
}
