using System.Globalization;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Domain.Tests.Common;

public class ValueObjectTests
{
    [Theory]
    [InlineData("abc-1234", "ABC1234")]
    [InlineData("ABC1D23", "ABC1D23")]
    [InlineData(" abc 1d23 ", "ABC1D23")]
    public void Plate_normalizes_valid_formats(string raw, string expected) => Assert.Equal(expected, Plate.From(raw).Value);

    [Theory]
    [InlineData("AB12345")]
    [InlineData("ABC12345")]
    [InlineData("")]
    [InlineData("ABCD123")]
    [InlineData("ABC1234\n")]
    public void Plate_rejects_invalid(string raw) => Assert.False(Plate.IsValid(raw));

    [Fact]
    public void Plate_From_throws_for_invalid_input() => Assert.Throws<ArgumentException>(() => Plate.From("AB12345"));

    [Fact]
    public void Plates_with_the_same_normalized_value_are_equal() => Assert.Equal(Plate.From("abc-1234"), Plate.From("ABC 1234"));

    [Theory]
    [InlineData(1997, 1998, 2026, true)]
    [InlineData(1998, 1998, 2026, true)]
    [InlineData(2027, 2028, 2026, true)]
    [InlineData(1998, 2000, 2026, false)]
    [InlineData(1899, 1899, 2026, false)]
    [InlineData(2028, 2028, 2026, false)]
    [InlineData(1998, 1997, 2026, false)]
    public void VehicleYear_rules(int fab, int model, int current, bool valid) => Assert.Equal(valid, VehicleYear.IsValid(fab, model, current));

    [Fact]
    public void VehicleYear_From_builds_valid_year()
    {
        var year = VehicleYear.From(1997, 1998, 2026);

        Assert.Equal(1997, year.Manufacture);
        Assert.Equal(1998, year.Model);
    }

    [Fact]
    public void VehicleYear_From_throws_for_invalid_input() => Assert.Throws<ArgumentException>(() => VehicleYear.From(1998, 2000, 2026));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(2_000_000, true)]
    [InlineData(2_000_001, false)]
    public void Odometer_range(int km, bool valid) => Assert.Equal(valid, Odometer.IsValid(km));

    [Fact]
    public void Odometer_keeps_km_when_valid() => Assert.Equal(123_456, new Odometer(123_456).Km);

    [Theory]
    [InlineData(-1)]
    [InlineData(2_000_001)]
    public void Odometer_ctor_throws_when_out_of_range(int km) => Assert.Throws<ArgumentOutOfRangeException>(() => new Odometer(km));

    [Theory]
    [InlineData("10.005", false)]
    [InlineData("-1", false)]
    [InlineData("0", true)]
    [InlineData("9999999999.99", true)]
    [InlineData("10000000000", false)]
    [InlineData("10.50", true)]
    public void Money_validity(string amount, bool valid) => Assert.Equal(valid, Money.IsValid(decimal.Parse(amount, CultureInfo.InvariantCulture)));

    [Fact]
    public void Money_times_rounds_to_cents()
    {
        Assert.Equal(99.99m, new Money(33.33m).Times(3).Amount);
        Assert.Equal(5.00m, new Money(10m).Times(0.5m).Amount);
    }

    [Fact]
    public void Money_times_rounds_midpoint_away_from_zero() => Assert.Equal(0.03m, new Money(0.01m).Times(2.5m).Amount);

    [Fact]
    public void Money_ctor_throws_when_invalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(10.005m));
    }

    [Fact]
    public void Money_zero_and_addition()
    {
        Assert.Equal(0m, Money.Zero.Amount);
        Assert.Equal(30.50m, (new Money(10.25m) + new Money(20.25m)).Amount);
    }
}
