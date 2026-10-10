using Garage.Domain.Common;
using Garage.Domain.Vehicles;

namespace Garage.Domain.Tests.Vehicles;

public class VehicleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddDays(30);
    private static readonly Guid OwnerId = Guid.Parse("0197b3a0-0000-7000-8000-000000000001");

    private static VehicleData Data(string? nickname = null) => new(
        Type: VehicleType.Car,
        Make: "Honda",
        Model: "Civic",
        Version: "Sedan LX 1.6 16V Mec. 4p",
        Generation: "EK",
        Year: new VehicleYear(1998, 1998),
        Fuel: Fuel.Gasoline,
        Nickname: nickname,
        Plate: Plate.From("ABC1D23"),
        Vin: "9BWZZZ377VT004251",
        Notes: "Projeto roxo",
        Catalog: new CatalogRef(VehicleType.Car, "014019-0", "1998-1"));

    private static Vehicle NewVehicle(int km = 90_000, string? nickname = null) =>
        Vehicle.Create(OwnerId, Data(nickname), new Odometer(km), Now);

    [Fact]
    public void Create_generates_version7_id()
    {
        var v = NewVehicle();

        Assert.Equal(7, v.Id.Version);
    }

    [Fact]
    public void Create_populates_owner_data_and_initial_odometer()
    {
        var v = NewVehicle();

        Assert.Equal(OwnerId, v.OwnerId);
        Assert.Equal(VehicleType.Car, v.Type);
        Assert.Equal("Honda", v.Make);
        Assert.Equal("Civic", v.Model);
        Assert.Equal("Sedan LX 1.6 16V Mec. 4p", v.Version);
        Assert.Equal("EK", v.Generation);
        Assert.Equal(new VehicleYear(1998, 1998), v.Year);
        Assert.Equal(Fuel.Gasoline, v.Fuel);
        Assert.Null(v.Nickname);
        Assert.Equal("ABC1D23", v.Plate!.Value);
        Assert.Equal("9BWZZZ377VT004251", v.Vin);
        Assert.Equal("Projeto roxo", v.Notes);
        Assert.Equal(new CatalogRef(VehicleType.Car, "014019-0", "1998-1"), v.Catalog);
        Assert.Equal(90_000, v.CurrentOdometer.Km);
        Assert.Equal(Now, v.OdometerUpdatedAt);
    }

    [Fact]
    public void Update_replaces_data_but_keeps_odometer()
    {
        var v = NewVehicle();
        var changed = Data("Roxinho") with
        {
            Make = "Toyota",
            Model = "Corolla",
            Plate = null,
            Catalog = null
        };

        v.Update(changed);

        Assert.Equal("Toyota", v.Make);
        Assert.Equal("Corolla", v.Model);
        Assert.Equal("Roxinho", v.Nickname);
        Assert.Null(v.Plate);
        Assert.Null(v.Catalog);
        Assert.Equal(OwnerId, v.OwnerId);
        Assert.Equal(90_000, v.CurrentOdometer.Km);
        Assert.Equal(Now, v.OdometerUpdatedAt);
    }

    [Fact]
    public void RegisterOdometer_higher_updates_km_and_timestamp()
    {
        var v = NewVehicle();

        v.RegisterOdometer(new Odometer(95_000), Later);

        Assert.Equal(95_000, v.CurrentOdometer.Km);
        Assert.Equal(Later, v.OdometerUpdatedAt);
    }

    [Theory]
    [InlineData(80_000)]
    [InlineData(90_000)]
    public void RegisterOdometer_lower_or_equal_is_ignored(int km)
    {
        var v = NewVehicle();

        v.RegisterOdometer(new Odometer(km), Later);

        Assert.Equal(90_000, v.CurrentOdometer.Km);
        Assert.Equal(Now, v.OdometerUpdatedAt);
    }

    [Fact]
    public void UpdateOdometer_higher_succeeds_and_updates_km_and_timestamp()
    {
        var v = NewVehicle();

        var result = v.UpdateOdometer(new Odometer(91_500), Later);

        Assert.True(result.IsSuccess);
        Assert.Equal(91_500, v.CurrentOdometer.Km);
        Assert.Equal(Later, v.OdometerUpdatedAt);
    }

    [Fact]
    public void UpdateOdometer_lower_fails_with_cannot_decrease()
    {
        var v = NewVehicle();

        var result = v.UpdateOdometer(new Odometer(89_999), Later);

        Assert.True(result.IsFailure);
        Assert.Equal("vehicle.odometer_cannot_decrease", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
        Assert.Equal(90_000, v.CurrentOdometer.Km);
        Assert.Equal(Now, v.OdometerUpdatedAt);
    }

    [Fact]
    public void UpdateOdometer_equal_succeeds()
    {
        var v = NewVehicle();

        var result = v.UpdateOdometer(new Odometer(90_000), Later);

        Assert.True(result.IsSuccess);
        Assert.Equal(90_000, v.CurrentOdometer.Km);
        Assert.Equal(Later, v.OdometerUpdatedAt);
    }

    [Fact]
    public void CorrectOdometer_below_max_record_fails()
    {
        var v = NewVehicle(km: 150_000);

        var result = v.CorrectOdometer(new Odometer(15_000), new Odometer(15_200), Later);

        Assert.True(result.IsFailure);
        Assert.Equal("vehicle.odometer_below_records", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
        Assert.Equal(15_200, Assert.IsType<int>(result.Error.Metadata!["maxRecordKm"]));
        Assert.Equal(150_000, v.CurrentOdometer.Km);
        Assert.Equal(Now, v.OdometerUpdatedAt);
    }

    [Fact]
    public void CorrectOdometer_to_max_record_decreases()
    {
        var v = NewVehicle(km: 150_000);

        var result = v.CorrectOdometer(new Odometer(15_200), new Odometer(15_200), Later);

        Assert.True(result.IsSuccess);
        Assert.Equal(15_200, v.CurrentOdometer.Km);
        Assert.Equal(Later, v.OdometerUpdatedAt);
    }

    [Fact]
    public void CorrectOdometer_without_records_accepts_any_value()
    {
        var v = NewVehicle(km: 150_000);

        var result = v.CorrectOdometer(new Odometer(10), maxRecordKm: null, Later);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, v.CurrentOdometer.Km);
        Assert.Equal(Later, v.OdometerUpdatedAt);
    }

    [Fact]
    public void CorrectOdometer_can_increase_above_current()
    {
        var v = NewVehicle();

        var result = v.CorrectOdometer(new Odometer(120_000), new Odometer(15_200), Later);

        Assert.True(result.IsSuccess);
        Assert.Equal(120_000, v.CurrentOdometer.Km);
    }

    [Fact]
    public void DisplayName_uses_nickname_or_make_model_year()
    {
        Assert.Equal("Honda Civic 1998", NewVehicle(nickname: null).DisplayName);
        Assert.Equal("Roxinho", NewVehicle(nickname: "Roxinho").DisplayName);
    }

    [Fact]
    public void DisplayName_uses_model_year_not_manufacture_year()
    {
        var v = Vehicle.Create(
            OwnerId,
            Data() with { Year = new VehicleYear(2020, 2021) },
            new Odometer(1_000),
            Now);

        Assert.Equal("Honda Civic 2021", v.DisplayName);
    }
}

public class VehicleErrorsTests
{
    [Fact]
    public void NotFound_is_a_not_found_error_in_pt_BR()
    {
        Assert.Equal("vehicle.not_found", VehicleErrors.NotFound.Code);
        Assert.Equal(ErrorType.NotFound, VehicleErrors.NotFound.Type);
        Assert.Equal("Veículo não encontrado.", VehicleErrors.NotFound.Message);
    }

    [Fact]
    public void OdometerCannotDecrease_is_a_domain_rule_error_in_pt_BR()
    {
        var error = VehicleErrors.OdometerCannotDecrease;

        Assert.Equal("vehicle.odometer_cannot_decrease", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("A quilometragem não pode diminuir. Se digitou errado, use a correção de km.", error.Message);
    }

    [Fact]
    public void OdometerBelowRecords_carries_max_in_metadata_and_formats_message_in_pt_BR()
    {
        var error = VehicleErrors.OdometerBelowRecords(15_200);

        Assert.Equal("vehicle.odometer_below_records", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal(15_200, Assert.IsType<int>(error.Metadata!["maxRecordKm"]));
        Assert.Equal("A km corrigida não pode ficar abaixo de 15.200 km, a maior km do histórico.", error.Message);
    }

    [Fact]
    public void CatalogNotLinked_is_a_not_found_error_in_pt_BR()
    {
        Assert.Equal("vehicle.catalog_not_linked", VehicleErrors.CatalogNotLinked.Code);
        Assert.Equal(ErrorType.NotFound, VehicleErrors.CatalogNotLinked.Type);
        Assert.Equal("Este veículo não está vinculado à FIPE.", VehicleErrors.CatalogNotLinked.Message);
    }
}
