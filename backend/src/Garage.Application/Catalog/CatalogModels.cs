using Garage.Domain.Vehicles;

namespace Garage.Application.Catalog;

// O vocabulário do catálogo é nosso: o formato da FIPE (códigos, nomes compostos, preço em texto) não passa daqui.

public sealed record CatalogMake(string Id, string Name);

/// <summary>
/// A FIPE junta modelo e versão em um nome só. <see cref="SuggestedModel"/> e <see cref="SuggestedVersion"/> são uma
/// sugestão (primeira palavra e o resto) para o formulário, onde o usuário os edita.
/// </summary>
public sealed record CatalogModel(string Id, string FullName, string SuggestedModel, string SuggestedVersion);

/// <summary><see cref="ModelYear"/> é nulo quando o nome do ano não começa por um ano (zero km, por exemplo).</summary>
public sealed record CatalogYear(string Id, int? ModelYear, Fuel Fuel, string Label);

public sealed record CatalogDetails(
    string FipeCode, string Make, string FullName, int? ModelYear, Fuel Fuel, decimal Price, string ReferenceMonth);

public sealed record CatalogPrice(decimal Price, string ReferenceMonth);
