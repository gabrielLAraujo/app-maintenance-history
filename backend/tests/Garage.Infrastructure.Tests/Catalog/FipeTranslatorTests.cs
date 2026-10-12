using System.Globalization;
using System.Text.Json;
using Garage.Domain.Vehicles;
using Garage.Infrastructure.Catalog;

namespace Garage.Infrastructure.Tests.Catalog;

public sealed class FipeTranslatorTests
{
    private static readonly string FixturesPath = Path.Combine(AppContext.BaseDirectory, "Catalog", "Fixtures");

    [Fact]
    public void ToMake_keeps_code_and_trims_name()
    {
        var make = FipeTranslator.ToMake(new FipeItem("25", "  Honda "));

        Assert.Equal("25", make.Id);
        Assert.Equal("Honda", make.Name);
    }

    [Theory]
    [InlineData("Civic Sedan LX 1.6 16V Mec. 4p", "Civic", "Sedan LX 1.6 16V Mec. 4p")]
    [InlineData("Civic CRX/ Targa VTi", "Civic", "CRX/ Targa VTi")]
    [InlineData("Fusca", "Fusca", "")]
    [InlineData("  Civic   Si  ", "Civic", "Si")]
    [InlineData("", "", "")]
    public void ToModel_splits_family_and_version(string name, string model, string version)
    {
        var result = FipeTranslator.ToModel(new FipeItem("1263", name));

        Assert.Equal("1263", result.Id);
        Assert.Equal(name.Trim(), result.FullName);
        Assert.Equal(model, result.SuggestedModel);
        Assert.Equal(version, result.SuggestedVersion);
    }

    [Theory]
    [InlineData("1998 Gasolina", 1998, Fuel.Gasoline)]
    [InlineData("2022 Diesel", 2022, Fuel.Diesel)]
    [InlineData("32000 Gasolina", null, Fuel.Gasoline)]
    [InlineData("2010 Álcool", 2010, Fuel.Ethanol)]
    [InlineData("Zero KM Flex", null, Fuel.Flex)]
    [InlineData("2020 Elétrico", 2020, Fuel.Electric)]
    [InlineData("2020 Híbrido", 2020, Fuel.Hybrid)]
    [InlineData("2020 GNV", 2020, Fuel.Other)]
    [InlineData("1900 Gasolina", 1900, Fuel.Gasoline)]
    [InlineData("2100 Gasolina", 2100, Fuel.Gasoline)]
    [InlineData("1899 Gasolina", null, Fuel.Gasoline)]
    [InlineData("2101 Gasolina", null, Fuel.Gasoline)]
    [InlineData("Gasolina 1998", null, Fuel.Gasoline)]
    public void ToYear_reads_year_and_fuel_from_name(string name, int? year, Fuel fuel)
    {
        var result = FipeTranslator.ToYear(new FipeItem("abc-1", name));

        Assert.Equal("abc-1", result.Id);
        Assert.Equal(year, result.ModelYear);
        Assert.Equal(fuel, result.Fuel);
        Assert.Equal(name, result.Label);
    }

    [Fact]
    public void ToYear_reads_the_year_from_the_name_not_from_the_code_suffix()
    {
        // O sufixo do código (-1, -3...) é o combustível da FIPE e não tem relação com o ano.
        var result = FipeTranslator.ToYear(new FipeItem("2022-3", "2022 Diesel"));

        Assert.Equal(2022, result.ModelYear);
    }

    [Theory]
    [InlineData("Gasolina", Fuel.Gasoline)]
    [InlineData("GASOLINA", Fuel.Gasoline)]
    [InlineData("Álcool", Fuel.Ethanol)]
    [InlineData("Etanol", Fuel.Ethanol)]
    [InlineData("Diesel", Fuel.Diesel)]
    [InlineData("Flex", Fuel.Flex)]
    [InlineData("Híbrido", Fuel.Hybrid)]
    [InlineData("Elétrico", Fuel.Electric)]
    [InlineData("Híbrido/Gasolina", Fuel.Hybrid)]
    [InlineData("Gasolina/Álcool", Fuel.Flex)]
    [InlineData("Álcool / Gasolina", Fuel.Flex)]
    [InlineData("GNV", Fuel.Other)]
    [InlineData("", Fuel.Other)]
    public void ParseFuel_matches_without_accent_or_case(string text, Fuel expected) =>
        Assert.Equal(expected, FipeTranslator.ParseFuel(text));

    [Theory]
    [InlineData("R$ 10.000,00", "10000.00")]
    [InlineData("R$ 1.234.567,89", "1234567.89")]
    [InlineData("R$ 0,50", "0.50")]
    [InlineData("R$99,90", "99.90")]
    public void ParsePrice_handles_brl(string text, string expected) =>
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), FipeTranslator.ParsePrice(text));

    [Fact]
    public void ParsePrice_accepts_a_non_breaking_space_after_the_currency_symbol() =>
        Assert.Equal(99.90m, FipeTranslator.ParsePrice("R$" + (char)0xA0 + "99,90"));

    [Theory]
    [InlineData("")]
    [InlineData("R$")]
    [InlineData("grátis")]
    public void ParsePrice_rejects_text_that_is_not_a_price(string text) =>
        Assert.Throws<FormatException>(() => FipeTranslator.ParsePrice(text));

    [Fact]
    public void ToDetails_translates_every_field()
    {
        var details = FipeTranslator.ToDetails(FipeJson.Detail(Fixture("detail-sample.json")));

        Assert.Equal("005340-6", details.FipeCode);
        Assert.Equal("Honda", details.Make);
        Assert.Equal("Civic Sedan LX 1.6 16V Mec. 4p", details.FullName);
        Assert.Equal(1998, details.ModelYear);
        Assert.Equal(Fuel.Gasoline, details.Fuel);
        Assert.Equal(10000.00m, details.Price);
        Assert.Equal("outubro de 2026", details.ReferenceMonth);
    }

    [Fact]
    public void ToDetails_drops_a_model_year_that_is_not_a_year()
    {
        var zeroKm = new FipeVehicleDetail("R$ 90.000,00", "Honda", "Civic", 32000, "Flex", "014017-1", "outubro de 2026");

        Assert.Null(FipeTranslator.ToDetails(zeroKm).ModelYear);
    }

    [Fact]
    public void Real_fipe_responses_translate_to_the_expected_values()
    {
        // Respostas reais da FIPE (v2) de outubro de 2026: Honda (25) → Civic Sedan LX (1263).
        var years = FipeJson.Items(Fixture("years-civic-real.json")).Select(FipeTranslator.ToYear).ToList();
        Assert.Equal(["2000-1", "1999-1", "1998-1", "1997-1"], years.Select(year => year.Id));
        Assert.Equal([2000, 1999, 1998, 1997], years.Select(year => year.ModelYear));
        Assert.All(years, year => Assert.Equal(Fuel.Gasoline, year.Fuel));

        var details = FipeTranslator.ToDetails(FipeJson.Detail(Fixture("detail-civic-real.json")));
        Assert.Equal("014017-1", details.FipeCode);
        Assert.Equal(17990.00m, details.Price);
        Assert.Equal(1998, details.ModelYear);
    }

    [Fact]
    public void All_fixtures_translate()
    {
        var files = Directory.GetFiles(FixturesPath, "*.json");
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var json = File.ReadAllText(file);

            if (name.StartsWith("models-", StringComparison.Ordinal))
                Assert.NotEmpty(FipeJson.Items(json).Select(FipeTranslator.ToModel).ToList());
            else if (name.StartsWith("years-", StringComparison.Ordinal))
                Assert.NotEmpty(FipeJson.Items(json).Select(FipeTranslator.ToYear).ToList());
            else if (name.StartsWith("detail-", StringComparison.Ordinal))
                Assert.NotNull(FipeTranslator.ToDetails(FipeJson.Detail(json)));
            else
                Assert.Fail($"Fixture sem tradutor conhecido: {name}. Use o prefixo models-, years- ou detail-.");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("null")]
    [InlineData("{\"code\":\"1\",\"name\":\"não é uma lista\"}")]
    [InlineData("[{\"code\":\"1\"}]")]
    [InlineData("[{\"code\":\"1\",\"name\":null}]")]
    public void Items_rejects_payloads_that_are_not_a_list_of_code_and_name(string json) =>
        Assert.Throws<JsonException>(() => FipeJson.Items(json));

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"price\":\"R$ 1,00\"}")]
    public void Detail_rejects_payloads_that_are_not_a_vehicle_detail(string json) =>
        Assert.Throws<JsonException>(() => FipeJson.Detail(json));

    [Fact]
    public void Detail_does_not_require_the_fields_the_translator_does_not_use()
    {
        // vehicleType e fuelAcronym não entram na tradução: se a FIPE deixar de mandá-los, a cotação continua valendo.
        const string json = """
            {"price":"R$ 10.000,00","brand":"Honda","model":"Civic Sedan LX 1.6 16V Mec. 4p","modelYear":1998,
             "fuel":"Gasolina","codeFipe":"005340-6","referenceMonth":"outubro de 2026"}
            """;

        var details = FipeTranslator.ToDetails(FipeJson.Detail(json));

        Assert.Equal("005340-6", details.FipeCode);
        Assert.Equal(10000.00m, details.Price);
        Assert.Equal(1998, details.ModelYear);
    }

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(FixturesPath, name));
}
