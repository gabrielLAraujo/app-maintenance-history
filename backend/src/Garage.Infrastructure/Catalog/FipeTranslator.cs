using System.Globalization;
using Garage.Application.Catalog;
using Garage.Domain.Vehicles;

namespace Garage.Infrastructure.Catalog;

/// <summary>
/// A camada anticorrupção: converte o formato da FIPE (spec 6.3) para o vocabulário do catálogo. Dados fora do formato
/// esperado — um preço que não é preço, por exemplo — viram <see cref="FormatException"/>, nunca um valor adivinhado.
/// </summary>
internal static class FipeTranslator
{
    private const int MinModelYear = 1900;
    private const int MaxModelYear = 2100;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static CatalogMake ToMake(FipeItem item) => new(item.Code, item.Name.Trim());

    /// <summary>"Civic Sedan LX 1.6" → modelo "Civic", versão "Sedan LX 1.6". É só uma sugestão: o usuário edita os dois.</summary>
    public static CatalogModel ToModel(FipeItem item)
    {
        var fullName = item.Name.Trim();
        var firstWordEnd = FirstWordEnd(fullName);

        return new CatalogModel(item.Code, fullName, fullName[..firstWordEnd], fullName[firstWordEnd..].Trim());
    }

    /// <summary>
    /// O ano vem do nome ("1998 Gasolina"), nunca do sufixo do código ("1998-1"). Um primeiro termo que não seja um ano
    /// (32000 é o zero km da FIPE; "Zero KM") deixa <c>ModelYear</c> nulo.
    /// </summary>
    public static CatalogYear ToYear(FipeItem item)
    {
        var label = item.Name.Trim();
        var firstWord = label[..FirstWordEnd(label)];

        int? modelYear = int.TryParse(firstWord, NumberStyles.None, CultureInfo.InvariantCulture, out var year) ? ToModelYear(year) : null;
        return new CatalogYear(item.Code, modelYear, ParseFuel(label), label);
    }

    public static CatalogDetails ToDetails(FipeVehicleDetail detail) => new(
        detail.CodeFipe,
        detail.Brand.Trim(),
        detail.Model.Trim(),
        ToModelYear(detail.ModelYear),
        ParseFuel(detail.Fuel),
        ParsePrice(detail.Price),
        detail.ReferenceMonth);

    /// <summary>"R$ 10.000,00" → 10000.00 (cultura pt-BR).</summary>
    public static decimal ParsePrice(string brl)
    {
        // O "R$" vem com espaço comum ou inseparável (U+00A0), conforme a origem.
        var amount = string.Concat(brl.Where(c => !char.IsWhiteSpace(c))).Replace("R$", string.Empty, StringComparison.Ordinal);
        return decimal.Parse(amount, NumberStyles.Number, PtBr);
    }

    /// <summary>
    /// Procura o combustível no texto inteiro, sem acento e sem diferenciar maiúsculas. Um texto com mais de um combustível
    /// vale pelo mais específico: híbrido e elétrico antes de tudo, gasolina com álcool é flex. Sem correspondência: Outro.
    /// </summary>
    public static Fuel ParseFuel(string text)
    {
        if (Mentions(text, "hibrido"))
            return Fuel.Hybrid;
        if (Mentions(text, "eletrico"))
            return Fuel.Electric;
        if (Mentions(text, "flex"))
            return Fuel.Flex;

        var gasoline = Mentions(text, "gasolina");
        var ethanol = Mentions(text, "alcool") || Mentions(text, "etanol");
        if (gasoline && ethanol)
            return Fuel.Flex;
        if (ethanol)
            return Fuel.Ethanol;
        if (Mentions(text, "diesel"))
            return Fuel.Diesel;

        return gasoline ? Fuel.Gasoline : Fuel.Other;
    }

    /// <summary>Onde termina a primeira palavra (o tamanho do texto, se for uma só).</summary>
    private static int FirstWordEnd(string text)
    {
        var end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
            end++;

        return end;
    }

    private static int? ToModelYear(int year) => year is >= MinModelYear and <= MaxModelYear ? year : null;

    private static bool Mentions(string text, string word) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
