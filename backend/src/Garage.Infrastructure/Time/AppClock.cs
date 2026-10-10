using Garage.Application.Abstractions.Ports;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Time;

internal sealed class AppClock(TimeProvider time, IOptions<AppOptions> options) : IAppClock
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTimeOffset Now => time.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _timeZone).DateTime);

    public int CurrentYear => Today.Year;
}
