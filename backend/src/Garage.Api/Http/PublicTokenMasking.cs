using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Garage.Api.Http;

/// <summary>
/// Tira dos logs o token dos links públicos: ele é a credencial do link (quem o tem vê o histórico do veículo). O caminho
/// com o token aparece no log de fim de requisição (<c>RequestPath</c>) e em todo evento gravado durante a requisição, pelo
/// escopo que o ASP.NET Core abre (também <c>RequestPath</c>). Toda propriedade de texto que comece por
/// <c>/api/public/{token}</c> ou <c>/p/{token}</c> sai com o segmento trocado por <c>***</c>; a mensagem renderizada é
/// montada a partir das propriedades, então sai mascarada também. Registrado por último, depois dos outros enrichers.
/// </summary>
internal sealed partial class PublicTokenMasking : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        List<LogEventProperty>? masked = null;
        foreach (var (name, value) in logEvent.Properties)
        {
            if (value is ScalarValue { Value: string text } && Mask(text) is var safe && safe != text)
                (masked ??= []).Add(new LogEventProperty(name, new ScalarValue(safe)));
        }

        // As propriedades não podem mudar durante a enumeração: troca depois.
        foreach (var property in masked ?? [])
            logEvent.AddOrUpdateProperty(property);
    }

    /// <summary><c>/api/public/{token}/attachments/{id}</c> → <c>/api/public/***/attachments/{id}</c>; outro texto volta igual.</summary>
    internal static string Mask(string path) => TokenSegment().Replace(path, "${prefix}***");

    // As rotas não diferenciam maiúsculas (/API/Public/... também chega ao endpoint).
    [GeneratedRegex("^(?<prefix>/api/public/|/p/)[^/?#]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenSegment();
}
