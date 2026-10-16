using Garage.Application.Abstractions.Ports;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Demo;

internal sealed class DemoLimits(IOptions<DemoVisitorOptions> options) : IDemoLimits
{
    public int MaxVehicles => options.Value.MaxVehicles;
}
