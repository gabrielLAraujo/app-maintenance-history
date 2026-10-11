using Garage.Domain.Common;

namespace Garage.Domain.Sharing;

public sealed class ShareLink : AggregateRoot
{
    /// <summary>Tamanho máximo do rótulo opcional (ex.: "Comprador"), spec 4.7.</summary>
    public const int MaxLabelLength = 60;

    // Construtor sem parâmetros apenas para a materialização do EF Core; os inicializadores
    // `null!` satisfazem a análise de nulos e são sempre sobrescritos na materialização.
    private ShareLink()
    {
    }

    public Guid VehicleId { get; private set; }

    public string Token { get; private set; } = null!;

    public string? Label { get; private set; }

    public bool IncludeCosts { get; private set; }

    public bool IncludePlate { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ShareLink Create(
        Guid vehicleId,
        string token,
        string? label,
        bool includeCosts,
        bool includePlate,
        DateTimeOffset? expiresAt,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            VehicleId = vehicleId,
            Token = token,
            Label = label,
            IncludeCosts = includeCosts,
            IncludePlate = includePlate,
            ExpiresAt = expiresAt,
            CreatedAt = now
        };

    /// <summary>Válido enquanto não foi revogado e ainda não expirou; no instante exato de <see cref="ExpiresAt"/> já é inválido.</summary>
    public bool IsValidAt(DateTimeOffset now) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);

    /// <summary>Idempotente: revogar de novo mantém o primeiro <see cref="RevokedAt"/>.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
