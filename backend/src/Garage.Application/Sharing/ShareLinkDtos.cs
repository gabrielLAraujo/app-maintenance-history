namespace Garage.Application.Sharing;

/// <summary>
/// Link público como o dono o vê. <see cref="Path"/> é a página pública no front (<c>/p/{token}</c>); <see cref="IsActive"/>
/// é a regra S1 avaliada agora (não revogado e não expirado).
/// </summary>
public sealed record ShareLinkDto(
    Guid Id,
    string Token,
    string Path,
    string? Label,
    bool IncludeCosts,
    bool IncludePlate,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset CreatedAt,
    bool IsActive);
