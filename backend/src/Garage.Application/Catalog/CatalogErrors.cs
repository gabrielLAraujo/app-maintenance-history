using Garage.Domain.Common;

namespace Garage.Application.Catalog;

public static class CatalogErrors
{
    /// <summary>Sem resposta da FIPE e sem nada no cache: o front passa ao preenchimento manual.</summary>
    public static readonly Error Unavailable = new(
        "catalog.unavailable",
        "Catálogo FIPE indisponível no momento. Preencha manualmente.",
        ErrorType.Unavailable);
}
