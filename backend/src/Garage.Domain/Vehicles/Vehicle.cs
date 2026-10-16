using Garage.Domain.Attachments;
using Garage.Domain.Common;

namespace Garage.Domain.Vehicles;

public sealed class Vehicle : AggregateRoot
{
    // Construtor sem parâmetros apenas para a materialização do EF Core; os inicializadores
    // `null!` satisfazem a análise de nulos e são sempre sobrescritos na materialização.
    private Vehicle()
    {
    }

    public Guid OwnerId { get; private set; }

    public VehicleType Type { get; private set; }

    public string Make { get; private set; } = null!;

    public string Model { get; private set; } = null!;

    public string? Version { get; private set; }

    public string? Generation { get; private set; }

    public VehicleYear Year { get; private set; }

    public Fuel Fuel { get; private set; }

    public string? Nickname { get; private set; }

    public Plate? Plate { get; private set; }

    public string? Vin { get; private set; }

    public string? Notes { get; private set; }

    public CatalogRef? Catalog { get; private set; }

    public Odometer CurrentOdometer { get; private set; }

    public DateTimeOffset OdometerUpdatedAt { get; private set; }

    /// <summary>Foto de capa: um anexo do tipo Foto deste veículo (C1), ou nulo.</summary>
    public Guid? CoverPhotoId { get; private set; }

    public string DisplayName => Nickname ?? $"{Make} {Model} {Year.Model}";

    public static Vehicle Create(Guid ownerId, VehicleData data, Odometer initialOdometer, DateTimeOffset now)
    {
        var vehicle = new Vehicle
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            CurrentOdometer = initialOdometer,
            OdometerUpdatedAt = now
        };

        vehicle.Apply(data);
        return vehicle;
    }

    public void Update(VehicleData data) => Apply(data);

    /// <summary>V1: uma leitura mais alta atualiza a km atual; uma leitura igual ou mais baixa é ignorada.</summary>
    public void RegisterOdometer(Odometer km, DateTimeOffset now)
    {
        if (km.Km > CurrentOdometer.Km)
            SetOdometer(km, now);
    }

    /// <summary>V2: atualização manual; a km não pode diminuir (igual é aceita).</summary>
    public Result UpdateOdometer(Odometer km, DateTimeOffset now)
    {
        if (km.Km < CurrentOdometer.Km)
            return VehicleErrors.OdometerCannotDecrease;

        SetOdometer(km, now);
        return Result.Success();
    }

    /// <summary>
    /// V3: correção explícita de digitação; permite diminuir, mas não abaixo da maior km do histórico
    /// (<paramref name="maxRecordKm"/>, nulo quando não há registros).
    /// </summary>
    public Result CorrectOdometer(Odometer km, Odometer? maxRecordKm, DateTimeOffset now)
    {
        if (maxRecordKm is { } max && km.Km < max.Km)
            return VehicleErrors.OdometerBelowRecords(max.Km);

        SetOdometer(km, now);
        return Result.Success();
    }

    /// <summary>C1: a capa precisa ser um anexo do tipo Foto do próprio veículo.</summary>
    public Result SetCoverPhoto(Attachment photo)
    {
        ArgumentNullException.ThrowIfNull(photo);

        if (photo.VehicleId != Id || photo.Kind != AttachmentKind.Photo)
            return VehicleErrors.CoverMustBeVehiclePhoto;

        CoverPhotoId = photo.Id;
        return Result.Success();
    }

    public void ClearCoverPhoto() => CoverPhotoId = null;

    private void SetOdometer(Odometer km, DateTimeOffset now)
    {
        CurrentOdometer = km;
        OdometerUpdatedAt = now;
    }

    private void Apply(VehicleData data)
    {
        Type = data.Type;
        Make = data.Make;
        Model = data.Model;
        Version = data.Version;
        Generation = data.Generation;
        Year = data.Year;
        Fuel = data.Fuel;
        Nickname = data.Nickname;
        Plate = data.Plate;
        Vin = data.Vin;
        Notes = data.Notes;
        Catalog = data.Catalog;
    }
}
