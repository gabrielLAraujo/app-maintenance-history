using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace Garage.Application.Sharing;

/// <summary>
/// Tokens dos links públicos (spec 4.7): 32 bytes de <see cref="RandomNumberGenerator"/> em base64url sem preenchimento,
/// ou seja, 43 caracteres de <c>[A-Za-z0-9_-]</c>. O token é a credencial do link: quem o tem vê a visão pública.
/// </summary>
public static class ShareTokens
{
    public const int Length = 43;

    private const int ByteCount = 32;

    private static readonly SearchValues<char> Alphabet =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteCount));

    /// <summary>
    /// <c>true</c> só para o formato de <see cref="New"/>: exatamente 43 caracteres do alfabeto base64url. Os handlers
    /// públicos recusam o resto antes de consultar o banco.
    /// </summary>
    public static bool LooksValid(string token) =>
        token is { Length: Length } && !token.AsSpan().ContainsAnyExcept(Alphabet);
}
