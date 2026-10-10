using Garage.Domain.Common;

namespace Garage.Domain.Sharing;

public static class ShareLinkErrors
{
    public static readonly Error NotFound = new(
        "share_link.not_found",
        "Link não encontrado ou expirado.",
        ErrorType.NotFound);
}
