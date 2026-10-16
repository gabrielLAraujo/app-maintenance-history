using Garage.Domain.Records;
using Garage.Domain.Vehicles;

namespace Garage.Application.Sharing;

// Visão pública (spec 4.8): read model próprio, montado campo a campo por allowlist e nunca a partir dos DTOs do dono.
// Nunca entram: chassi, observações, documentos, fotos privadas, lembretes, valor FIPE, dados do dono e ids internos
// (só o id das fotos públicas, que compõe o caminho de download).

/// <summary>
/// Um veículo pelo link. <see cref="Plate"/> só com <c>IncludePlate</c>; <see cref="IncludesCosts"/> diz se os custos dos
/// registros vêm preenchidos. <see cref="CoverPhoto"/> só se a foto de capa for pública. <see cref="Records"/> é uma
/// página do histórico (mais recentes primeiro).
/// </summary>
public sealed record PublicVehicleDto(
    string Make,
    string Model,
    string? Version,
    string? Generation,
    int ManufactureYear,
    int ModelYear,
    Fuel Fuel,
    string? Nickname,
    string? Plate,
    int CurrentOdometer,
    bool IncludesCosts,
    PublicPhotoDto? CoverPhoto,
    IReadOnlyList<PublicRecordDto> Records,
    string? NextCursor);

/// <summary>Um registro do histórico. <see cref="LaborCost"/> e <see cref="TotalCost"/> só com <c>IncludeCosts</c>.</summary>
public sealed record PublicRecordDto(
    RecordType Type,
    DateOnly Date,
    int? Odometer,
    Category Category,
    string Title,
    string? Description,
    string? PerformedBy,
    string? ReplacedOriginalPart,
    IReadOnlyList<PublicPartDto> Parts,
    decimal? LaborCost,
    decimal? TotalCost,
    IReadOnlyList<PublicPhotoDto> Photos);

/// <summary>Uma peça do registro. <see cref="UnitPrice"/> só com <c>IncludeCosts</c>.</summary>
public sealed record PublicPartDto(
    string Name,
    string? Brand,
    string? PartNumber,
    decimal Quantity,
    PartUnit Unit,
    decimal? UnitPrice);

/// <summary>Foto pública do registro; <see cref="Path"/> é o download pelo link (<c>/api/public/{token}/attachments/{id}</c>).</summary>
public sealed record PublicPhotoDto(Guid Id, string Path);
