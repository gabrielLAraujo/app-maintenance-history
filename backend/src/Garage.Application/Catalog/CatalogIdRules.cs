using FluentValidation;

namespace Garage.Application.Catalog;

internal static class CatalogIdRules
{
    // Os códigos FIPE (marca, modelo, ano) viram segmentos da URL consultada na FIPE e chave do cache. Letras, dígitos e hífen
    // cobrem todos eles ("25", "1263", "1998-1") e deixam de fora o que mudaria a URL: barra, ponto, "?", "#", "%", espaço.
    // O tamanho é o das colunas de código FIPE do veículo.
    private const int MaxLength = 20;

    public static IRuleBuilderOptions<T, string> MustBeCatalogId<T>(this IRuleBuilder<T, string> rule, string message) =>
        rule.Must(IsValid).WithMessage(message);

    private static bool IsValid(string? id) =>
        id is { Length: > 0 and <= MaxLength } && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}
