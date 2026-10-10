using Garage.Domain.Common;
using Garage.Domain.Records;

namespace Garage.Domain.Tests.Records;

public class ServiceRecordTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid VehicleId = Guid.Parse("0197b3a0-0000-7000-8000-000000000002");

    private static PartData OilFilter(decimal quantity = 1m, decimal? price = 45.90m) => new(
        Name: "Filtro de óleo",
        Brand: "Mann",
        PartNumber: "W712/95",
        Quantity: quantity,
        Unit: PartUnit.Unit,
        UnitPrice: price is { } p ? new Money(p) : null);

    private static RecordDetails Details(RecordType type = RecordType.Maintenance) => new(
        Type: type,
        Date: Today,
        Odometer: new Odometer(85_000),
        Title: "Troca de óleo e filtro",
        Description: "Óleo 5W30 sintético",
        Category: Category.Engine,
        PerformedBy: "Oficina do Zé",
        LaborCost: new Money(80m),
        ReplacedOriginalPart: null,
        Parts: [OilFilter()]);

    private static Result<ServiceRecord> Create(
        RecordDetails? details = null,
        OdometerBounds? bounds = null,
        IReadOnlyList<Guid>? fulfilledReminderIds = null) =>
        ServiceRecord.Create(
            VehicleId,
            details ?? Details(),
            bounds ?? OdometerBounds.None,
            fulfilledReminderIds ?? [],
            Today,
            Now);

    private static ServiceRecord NewRecord(RecordDetails? details = null) => Create(details).Value;

    [Fact]
    public void Create_populates_fields_and_generates_version7_ids()
    {
        var record = Create(Details()).Value;

        Assert.Equal(7, record.Id.Version);
        Assert.Equal(VehicleId, record.VehicleId);
        Assert.Equal(RecordType.Maintenance, record.Type);
        Assert.Equal(Today, record.Date);
        Assert.Equal(85_000, record.Odometer!.Value.Km);
        Assert.Equal("Troca de óleo e filtro", record.Title);
        Assert.Equal("Óleo 5W30 sintético", record.Description);
        Assert.Equal(Category.Engine, record.Category);
        Assert.Equal("Oficina do Zé", record.PerformedBy);
        Assert.Equal(new Money(80m), record.LaborCost);
        Assert.Null(record.ReplacedOriginalPart);
        Assert.Equal(Now, record.CreatedAt);

        var part = Assert.Single(record.Parts);
        Assert.Equal(7, part.Id.Version);
        Assert.Equal("Filtro de óleo", part.Name);
        Assert.Equal("Mann", part.Brand);
        Assert.Equal("W712/95", part.PartNumber);
        Assert.Equal(1m, part.Quantity);
        Assert.Equal(PartUnit.Unit, part.Unit);
        Assert.Equal(new Money(45.90m), part.UnitPrice);
    }

    [Fact]
    public void Create_with_future_date_fails()
    {
        var result = Create(Details() with { Date = new DateOnly(2026, 10, 11) });

        Assert.True(result.IsFailure);
        Assert.Equal("record.date_in_future", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Create_dated_today_succeeds()
    {
        var result = Create(Details() with { Date = Today });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ReplacedOriginalPart_on_maintenance_fails()
    {
        var result = Create(Details(RecordType.Maintenance) with { ReplacedOriginalPart = "Escapamento original" });

        Assert.True(result.IsFailure);
        Assert.Equal("record.replaced_part_only_for_modification", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
    }

    [Fact]
    public void ReplacedOriginalPart_on_modification_succeeds()
    {
        var result = Create(Details(RecordType.Modification) with { ReplacedOriginalPart = "Escapamento original" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Escapamento original", result.Value.ReplacedOriginalPart);
    }

    [Fact]
    public void Create_validates_R1_then_R2_then_R3()
    {
        var violatesAll = Details(RecordType.Maintenance) with
        {
            Date = new DateOnly(2026, 10, 11),
            ReplacedOriginalPart = "Escapamento original",
            Odometer = new Odometer(10_000)
        };
        var bounds = new OdometerBounds(80_000, 90_000);

        var r1 = Create(violatesAll, bounds);
        var r2 = Create(violatesAll with { Date = Today }, bounds);
        var r3 = Create(violatesAll with { Date = Today, ReplacedOriginalPart = null }, bounds);

        Assert.Equal("record.date_in_future", r1.Error!.Code);
        Assert.Equal("record.replaced_part_only_for_modification", r2.Error!.Code);
        Assert.Equal("record.odometer_out_of_order", r3.Error!.Code);
    }

    [Theory]
    [InlineData(85_000, 80_000, 90_000)]
    [InlineData(80_000, 80_000, 90_000)]
    [InlineData(90_000, 80_000, 90_000)]
    [InlineData(50_000, null, null)]
    [InlineData(85_000, 80_000, null)]
    public void Odometer_within_bounds_succeeds(int km, int? before, int? after)
    {
        var result = Create(Details() with { Odometer = new Odometer(km) }, new OdometerBounds(before, after));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(79_999, 80_000, 90_000)]
    [InlineData(90_001, 80_000, 90_000)]
    [InlineData(79_999, 80_000, null)]
    [InlineData(90_001, null, 90_000)]
    public void Odometer_out_of_bounds_fails_with_metadata(int km, int? before, int? after)
    {
        var result = Create(Details() with { Odometer = new Odometer(km) }, new OdometerBounds(before, after));

        Assert.True(result.IsFailure);
        Assert.Equal("record.odometer_out_of_order", result.Error!.Code);
        Assert.Equal(ErrorType.DomainRule, result.Error.Type);
        Assert.Equal(before, (int?)result.Error.Metadata!["maxKmBefore"]);
        Assert.Equal(after, (int?)result.Error.Metadata["minKmAfter"]);
    }

    [Fact]
    public void Record_without_odometer_ignores_bounds()
    {
        var result = Create(Details() with { Odometer = null }, new OdometerBounds(80_000, 90_000));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Odometer);
    }

    [Fact]
    public void Create_raises_ServiceRecordAdded_with_fulfilled_ids()
    {
        var fulfilled = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };

        var record = Create(fulfilledReminderIds: fulfilled).Value;

        var added = Assert.IsType<ServiceRecordAdded>(Assert.Single(record.DomainEvents));
        Assert.Equal(record.Id, added.RecordId);
        Assert.Equal(VehicleId, added.VehicleId);
        Assert.Equal(Today, added.Date);
        Assert.Equal(85_000, added.Odometer!.Value.Km);
        Assert.Equal(fulfilled, added.FulfilledReminderIds);
    }

    [Fact]
    public void TotalCost_sums_parts_and_labor()
    {
        var details = Details() with
        {
            LaborCost = new Money(80m),
            Parts =
            [
                OilFilter(quantity: 2m, price: 45.90m),
                OilFilter(quantity: 1m, price: 120.00m)
            ]
        };

        var record = NewRecord(details);

        Assert.Equal(new Money(91.80m), record.Parts[0].LineTotal);
        Assert.Equal(new Money(120.00m), record.Parts[1].LineTotal);
        Assert.Equal(new Money(291.80m), record.TotalCost);
    }

    [Fact]
    public void TotalCost_treats_missing_prices_and_labor_as_zero()
    {
        var details = Details() with
        {
            LaborCost = null,
            Parts = [OilFilter(quantity: 3m, price: null)]
        };

        var record = NewRecord(details);

        Assert.Equal(Money.Zero, record.Parts[0].LineTotal);
        Assert.Equal(Money.Zero, record.TotalCost);
    }

    [Fact]
    public void Update_replaces_fields_and_parts()
    {
        var record = NewRecord();
        var oldPartId = record.Parts[0].Id;
        var changed = Details(RecordType.Modification) with
        {
            Date = new DateOnly(2026, 10, 1),
            Odometer = null,
            Title = "Escapamento esportivo",
            Description = null,
            PerformedBy = null,
            LaborCost = null,
            ReplacedOriginalPart = "Escapamento original",
            Parts =
            [
                new PartData("Escapamento inox", "Esportiva", null, 1m, PartUnit.Unit, new Money(900m)),
                new PartData("Abraçadeira", null, null, 2m, PartUnit.Unit, null)
            ]
        };

        var result = record.Update(changed, OdometerBounds.None, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(RecordType.Modification, record.Type);
        Assert.Equal(new DateOnly(2026, 10, 1), record.Date);
        Assert.Null(record.Odometer);
        Assert.Equal("Escapamento esportivo", record.Title);
        Assert.Null(record.Description);
        Assert.Null(record.PerformedBy);
        Assert.Null(record.LaborCost);
        Assert.Equal("Escapamento original", record.ReplacedOriginalPart);
        Assert.Equal(["Escapamento inox", "Abraçadeira"], record.Parts.Select(p => p.Name));
        Assert.DoesNotContain(record.Parts, p => p.Id == oldPartId);
        Assert.Equal(new Money(900m), record.TotalCost);
        Assert.Equal(VehicleId, record.VehicleId);
        Assert.Equal(Now, record.CreatedAt);
    }

    [Fact]
    public void Update_with_invalid_data_keeps_state()
    {
        var record = NewRecord();
        var originalPartIds = record.Parts.Select(p => p.Id).ToArray();

        var result = record.Update(
            Details() with { Date = new DateOnly(2026, 10, 11), Title = "Outro título", Parts = [] },
            OdometerBounds.None,
            Today);

        Assert.True(result.IsFailure);
        Assert.Equal("record.date_in_future", result.Error!.Code);
        Assert.Equal("Troca de óleo e filtro", record.Title);
        Assert.Equal(Today, record.Date);
        Assert.Equal(originalPartIds, record.Parts.Select(p => p.Id));
        Assert.Equal(new Money(125.90m), record.TotalCost);
    }

    [Fact]
    public void Update_with_odometer_out_of_order_keeps_state()
    {
        var record = NewRecord();

        var result = record.Update(
            Details() with { Odometer = new Odometer(70_000), Title = "Outro título" },
            new OdometerBounds(80_000, 90_000),
            Today);

        Assert.True(result.IsFailure);
        Assert.Equal("record.odometer_out_of_order", result.Error!.Code);
        Assert.Equal("Troca de óleo e filtro", record.Title);
        Assert.Equal(85_000, record.Odometer!.Value.Km);
    }

    [Fact]
    public void Update_with_replaced_part_on_maintenance_fails()
    {
        var record = NewRecord();

        var result = record.Update(
            Details(RecordType.Maintenance) with { ReplacedOriginalPart = "Escapamento original" },
            OdometerBounds.None,
            Today);

        Assert.True(result.IsFailure);
        Assert.Equal("record.replaced_part_only_for_modification", result.Error!.Code);
        Assert.Null(record.ReplacedOriginalPart);
    }

    [Fact]
    public void Update_does_not_raise_events()
    {
        var record = NewRecord();
        record.ClearDomainEvents();

        var result = record.Update(Details() with { Title = "Revisão" }, OdometerBounds.None, Today);

        Assert.True(result.IsSuccess);
        Assert.Empty(record.DomainEvents);
    }
}

public class ServiceRecordErrorsTests
{
    [Fact]
    public void NotFound_is_a_not_found_error_in_pt_BR()
    {
        Assert.Equal("record.not_found", ServiceRecordErrors.NotFound.Code);
        Assert.Equal(ErrorType.NotFound, ServiceRecordErrors.NotFound.Type);
        Assert.Equal("Registro não encontrado.", ServiceRecordErrors.NotFound.Message);
    }

    [Fact]
    public void DateInFuture_is_a_domain_rule_error_in_pt_BR()
    {
        var error = ServiceRecordErrors.DateInFuture;

        Assert.Equal("record.date_in_future", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("A data do registro não pode estar no futuro.", error.Message);
    }

    [Fact]
    public void ReplacedPartOnlyForModification_is_a_domain_rule_error_in_pt_BR()
    {
        var error = ServiceRecordErrors.ReplacedPartOnlyForModification;

        Assert.Equal("record.replaced_part_only_for_modification", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal("Peça original substituída só vale para modificações.", error.Message);
    }

    [Theory]
    [InlineData(80_000, 90_000, "A km deve ficar entre 80.000 e 90.000 km para manter a ordem do histórico.")]
    [InlineData(80_000, null, "A km deve ser de pelo menos 80.000 km para manter a ordem do histórico.")]
    [InlineData(null, 90_000, "A km deve ser de no máximo 90.000 km para manter a ordem do histórico.")]
    public void OdometerOutOfOrder_formats_the_valid_range_in_pt_BR(int? before, int? after, string message)
    {
        var error = ServiceRecordErrors.OdometerOutOfOrder(before, after);

        Assert.Equal("record.odometer_out_of_order", error.Code);
        Assert.Equal(ErrorType.DomainRule, error.Type);
        Assert.Equal(message, error.Message);
        Assert.Equal(before, (int?)error.Metadata!["maxKmBefore"]);
        Assert.Equal(after, (int?)error.Metadata["minKmAfter"]);
    }
}
