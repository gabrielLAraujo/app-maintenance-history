namespace Garage.Infrastructure.Catalog;

internal sealed class FipeOptions
{
    public const string SectionName = "Fipe";

    /// <summary>URL base da API da FIPE (Parallelum). As rotas consultadas são relativas a ela.</summary>
    public string BaseUrl { get; set; } = "https://fipe.parallelum.com.br/api/v2/";

    /// <summary>
    /// Token de assinatura, opcional: sem ele a cota é de 500 consultas por dia; com o token gratuito, 1.000.
    /// Vai no cabeçalho <c>X-Subscription-Token</c>.
    /// </summary>
    public string? Token { get; set; }
}
