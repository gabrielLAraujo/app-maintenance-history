using Garage.Domain.Sharing;

namespace Garage.Application.Sharing;

internal static class ShareLinkMapping
{
    public static ShareLinkDto ToDto(this ShareLink link, DateTimeOffset now) => new(
        link.Id,
        link.Token,
        PagePath(link.Token),
        link.Label,
        link.IncludeCosts,
        link.IncludePlate,
        link.ExpiresAt,
        link.RevokedAt,
        link.CreatedAt,
        link.IsValidAt(now));

    /// <summary>Página pública no front (rota <c>/p/:token</c>). O token é base64url: não precisa de escape na URL.</summary>
    public static string PagePath(string token) => $"/p/{token}";

    /// <summary>Download de uma foto pública pelo link.</summary>
    public static string PhotoPath(string token, Guid attachmentId) => $"/api/public/{token}/attachments/{attachmentId}";
}
