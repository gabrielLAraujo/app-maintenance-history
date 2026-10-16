using Garage.Domain.Records;

namespace Garage.Application.Spending;

/// <summary>Gasto de um mês; <see cref="Month"/> no formato <c>yyyy-MM</c>.</summary>
public sealed record MonthCostDto(string Month, decimal Cost);

/// <summary>Gasto de uma categoria.</summary>
public sealed record CategoryCostDto(Category Category, decimal Cost);
