using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Garage.Application.Records;

/// <summary>
/// Cursor opaco da listagem de registros: a posição (data, criação, id) do último item entregue, em base64url de
/// <c>yyyy-MM-dd|O|guid</c>. A próxima página continua estritamente depois dela na ordem <c>date, created_at, id</c> DESC.
/// </summary>
public static class RecordCursor
{
    private const char Separator = '|';
    private const string DateFormat = "yyyy-MM-dd";
    private const string InstantFormat = "O";
    private const string IdFormat = "D";

    // Um cursor válido tem ~90 caracteres; o limite só evita decodificar textos arbitrariamente grandes.
    private const int MaxLength = 256;

    public static string Encode(DateOnly date, DateTimeOffset createdAt, Guid id)
    {
        var raw = string.Join(
            Separator,
            date.ToString(DateFormat, CultureInfo.InvariantCulture),
            createdAt.ToUniversalTime().ToString(InstantFormat, CultureInfo.InvariantCulture),
            id.ToString(IdFormat, CultureInfo.InvariantCulture));

        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>Decodifica um cursor de <see cref="Encode"/>; <c>false</c> para vazio, base64url inválido ou conteúdo malformado.</summary>
    public static bool TryDecode(string? cursor, out (DateOnly Date, DateTimeOffset CreatedAt, Guid Id) value)
    {
        value = default;
        if (string.IsNullOrEmpty(cursor) || cursor.Length > MaxLength)
            return false;

        Span<byte> bytes = stackalloc byte[Base64Url.GetMaxDecodedLength(cursor.Length)];
        if (Base64Url.DecodeFromChars(cursor, bytes, out _, out var written) != OperationStatus.Done)
            return false;

        var fields = Encoding.UTF8.GetString(bytes[..written]).Split(Separator);
        if (fields.Length != 3
            || !DateOnly.TryParseExact(fields[0], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || !DateTimeOffset.TryParseExact(fields[1], InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdAt)
            || !Guid.TryParseExact(fields[2], IdFormat, out var id))
            return false;

        // O Npgsql só grava timestamptz com offset zero.
        value = (date, createdAt.ToUniversalTime(), id);
        return true;
    }
}
