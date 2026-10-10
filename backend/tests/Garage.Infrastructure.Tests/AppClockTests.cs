using System.Globalization;
using Garage.Infrastructure.Time;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Garage.Infrastructure.Tests;

public class AppClockTests
{
    [Theory]
    [InlineData("2026-10-11T02:30:00Z", "2026-10-10")]
    [InlineData("2026-10-11T03:00:00Z", "2026-10-11")]
    public void Today_uses_sao_paulo(string utcNow, string expected)
    {
        var clock = CreateClock(utcNow);

        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), clock.Today);
    }

    [Theory]
    [InlineData("2027-01-01T02:59:00Z", 2026)]
    [InlineData("2027-01-01T03:00:00Z", 2027)]
    public void CurrentYear_uses_sao_paulo(string utcNow, int expected) =>
        Assert.Equal(expected, CreateClock(utcNow).CurrentYear);

    [Fact]
    public void Now_is_the_utc_instant_of_the_time_provider()
    {
        var clock = CreateClock("2026-10-11T02:30:00Z");

        Assert.Equal(new DateTimeOffset(2026, 10, 11, 2, 30, 0, TimeSpan.Zero), clock.Now);
        Assert.Equal(TimeSpan.Zero, clock.Now.Offset);
    }

    private static AppClock CreateClock(string utcNow) => new(
        new FakeTimeProvider(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture)),
        Options.Create(new AppOptions()));
}
