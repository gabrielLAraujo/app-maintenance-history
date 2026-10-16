using System.Globalization;
using Garage.Domain.Records;

namespace Garage.Application.Spending;

/// <summary>
/// Agregações dos resumos de gastos (spec 5), feitas em memória sobre os registros já carregados (spec 5.5). O custo de um
/// registro é <see cref="ServiceRecord.TotalCost"/>: registro sem custo soma 0 e conta normalmente nas quantidades.
/// </summary>
internal static class SpendingMath
{
    public static decimal Total(IEnumerable<ServiceRecord> records) => records.Sum(record => record.TotalCost.Amount);

    /// <summary><paramref name="count"/> meses consecutivos a partir do mês de <paramref name="firstMonth"/>, zerados quando não há gasto.</summary>
    public static IReadOnlyList<MonthCostDto> Months(IEnumerable<ServiceRecord> records, DateOnly firstMonth, int count)
    {
        var start = new DateOnly(firstMonth.Year, firstMonth.Month, 1);
        var costs = records
            .GroupBy(record => (record.Date.Year, record.Date.Month))
            .ToDictionary(group => group.Key, Total);

        return Enumerable.Range(0, count)
            .Select(offset => start.AddMonths(offset))
            .Select(month => new MonthCostDto(
                month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                costs.GetValueOrDefault((month.Year, month.Month))))
            .ToList();
    }

    /// <summary>As categorias com custo maior que zero, da maior para a menor.</summary>
    public static IReadOnlyList<CategoryCostDto> ByCategory(IEnumerable<ServiceRecord> records) =>
        records
            .GroupBy(record => record.Category)
            .Select(group => new CategoryCostDto(group.Key, Total(group)))
            .Where(category => category.Cost > 0)
            .OrderByDescending(category => category.Cost)
            .ThenBy(category => category.Category)
            .ToList();

    /// <summary>O registro mais recente, na mesma ordem do histórico (data, criação, id).</summary>
    public static ServiceRecord? Latest(IEnumerable<ServiceRecord> records) =>
        records
            .OrderByDescending(record => record.Date)
            .ThenByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.Id)
            .FirstOrDefault();
}
