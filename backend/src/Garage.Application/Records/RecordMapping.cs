using Garage.Domain.Common;
using Garage.Domain.Records;

namespace Garage.Application.Records;

internal static class RecordMapping
{
    /// <summary>
    /// Converte a entrada, que o validator já aprovou, nos dados do domínio. Os textos são aparados e os opcionais em
    /// branco viram nulos (uma peça original substituída em branco numa manutenção não pode disparar R2).
    /// </summary>
    public static RecordDetails ToDetails(this RecordInput input) => new(
        input.Type,
        input.Date,
        input.Odometer is { } km ? new Odometer(km) : null,
        input.Title.Trim(),
        Clean(input.Description),
        input.Category,
        Clean(input.PerformedBy),
        input.LaborCost is { } labor ? new Money(labor) : null,
        Clean(input.ReplacedOriginalPart),
        input.Parts.Select(ToData).ToList());

    /// <summary>Total de uma peça lida do banco, pela mesma regra de <see cref="Part.LineTotal"/> (zero sem preço).</summary>
    public static Money LineTotal(decimal quantity, Money? unitPrice) => unitPrice?.Times(quantity) ?? Money.Zero;

    /// <summary>Custo total de um registro lido do banco, pela mesma regra de <see cref="ServiceRecord.TotalCost"/>.</summary>
    public static Money TotalCost(Money? laborCost, IEnumerable<Money> lineTotals) =>
        lineTotals.Aggregate(laborCost ?? Money.Zero, (sum, lineTotal) => sum + lineTotal);

    private static PartData ToData(PartInput part) => new(
        part.Name.Trim(),
        Clean(part.Brand),
        Clean(part.PartNumber),
        part.Quantity,
        part.Unit,
        part.UnitPrice is { } price ? new Money(price) : null);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
