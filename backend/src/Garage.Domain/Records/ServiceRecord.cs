using Garage.Domain.Common;

namespace Garage.Domain.Records;

public sealed class ServiceRecord : AggregateRoot
{
    private readonly List<Part> _parts = [];

    // Construtor sem parâmetros apenas para a materialização do EF Core; os inicializadores
    // `null!` satisfazem a análise de nulos e são sempre sobrescritos na materialização.
    private ServiceRecord()
    {
    }

    public Guid VehicleId { get; private set; }

    public RecordType Type { get; private set; }

    public DateOnly Date { get; private set; }

    public Odometer? Odometer { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public Category Category { get; private set; }

    public string? PerformedBy { get; private set; }

    public Money? LaborCost { get; private set; }

    public string? ReplacedOriginalPart { get; private set; }

    public IReadOnlyList<Part> Parts => _parts;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Soma do total de cada peça mais a mão de obra (calculado, não persistido).</summary>
    public Money TotalCost => _parts.Aggregate(LaborCost ?? Money.Zero, (sum, part) => sum + part.LineTotal);

    /// <summary>Valida R1, R2 e R3 nesta ordem e emite <see cref="ServiceRecordAdded"/> (R5).</summary>
    public static Result<ServiceRecord> Create(
        Guid vehicleId,
        RecordDetails details,
        OdometerBounds bounds,
        IReadOnlyList<Guid> fulfilledReminderIds,
        DateOnly today,
        DateTimeOffset now)
    {
        var validation = Validate(details, bounds, today);
        if (validation.IsFailure)
            return validation.Error!;

        var record = new ServiceRecord
        {
            Id = Guid.CreateVersion7(),
            VehicleId = vehicleId,
            CreatedAt = now
        };

        record.Apply(details);
        record.Raise(new ServiceRecordAdded(
            record.Id,
            vehicleId,
            details.Date,
            details.Odometer,
            fulfilledReminderIds.ToArray()));

        return record;
    }

    /// <summary>Mesmas regras do Create; substitui as peças, não emite evento e, em caso de falha, não altera o estado.</summary>
    public Result Update(RecordDetails details, OdometerBounds bounds, DateOnly today)
    {
        var validation = Validate(details, bounds, today);
        if (validation.IsFailure)
            return validation;

        Apply(details);
        return Result.Success();
    }

    private static Result Validate(RecordDetails details, OdometerBounds bounds, DateOnly today)
    {
        if (details.Date > today)
            return ServiceRecordErrors.DateInFuture;

        if (details.ReplacedOriginalPart is not null && details.Type != RecordType.Modification)
            return ServiceRecordErrors.ReplacedPartOnlyForModification;

        if (details.Odometer is { } odometer && IsOutOfOrder(odometer, bounds))
            return ServiceRecordErrors.OdometerOutOfOrder(bounds.MaxKmBefore, bounds.MinKmAfter);

        return Result.Success();
    }

    private static bool IsOutOfOrder(Odometer odometer, OdometerBounds bounds) =>
        (bounds.MaxKmBefore is { } maxBefore && odometer.Km < maxBefore) ||
        (bounds.MinKmAfter is { } minAfter && odometer.Km > minAfter);

    private void Apply(RecordDetails details)
    {
        var parts = details.Parts.Select(Part.From).ToList();

        Type = details.Type;
        Date = details.Date;
        Odometer = details.Odometer;
        Title = details.Title;
        Description = details.Description;
        Category = details.Category;
        PerformedBy = details.PerformedBy;
        LaborCost = details.LaborCost;
        ReplacedOriginalPart = details.ReplacedOriginalPart;

        _parts.Clear();
        _parts.AddRange(parts);
    }
}
